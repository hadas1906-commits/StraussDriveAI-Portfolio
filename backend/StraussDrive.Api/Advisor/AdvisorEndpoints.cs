using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using StraussDrive.Import;
using StraussDrive.Domain;
namespace StraussDrive.Api.Advisor;

public static class AdvisorEndpoints
{
    public const string EmployeeScheme = "AdvisorEmployee", RepresentativeScheme = "AdvisorRepresentative";
    public static void AddAdvisor(this WebApplicationBuilder builder)
    {
        // Local development opts in by environment; every production demo must opt in explicitly.
        if (builder.Configuration["Advisor:DemoAuthEnabled"] is null)
            builder.Configuration["Advisor:DemoAuthEnabled"] = builder.Environment.IsDevelopment().ToString();
        var database = Path.GetFullPath(builder.Configuration["Advisor:DatabasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, ".local", "advisor.db"));
        var directory = Path.GetDirectoryName(database)!;
        var keyDirectory = Path.Combine(directory, Path.GetFileNameWithoutExtension(database) + ".keys");
        var existingKeys = Directory.Exists(keyDirectory); Directory.CreateDirectory(keyDirectory);
        if (!existingKeys && !OperatingSystem.IsWindows()) File.SetUnixFileMode(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        builder.Services.AddDataProtection().SetApplicationName("StraussDrive.Advisor.v1").PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
        builder.Services.AddSingleton<AdvisorStore>(); builder.Services.AddSingleton<IAdvisorTools, AdvisorTools>();
        builder.Services.AddSingleton<DemoConversationProvider>();
        builder.Services.AddSingleton<EvaluationBudget>();
        // Portfolio: no HTTP AI provider is registered or distributed.
        builder.Configuration["Advisor:ForceDeterministic"] = "true";
        builder.Services.AddSingleton<IConversationProvider>(sp => sp.GetRequiredService<DemoConversationProvider>());

        builder.Services.AddSingleton<ConversationAgent>();
        builder.Services.AddAuthentication(EmployeeScheme).AddCookie(EmployeeScheme, options => Cookie(options, "sd.employee", "/api/advisor", builder.Environment))
            .AddCookie(RepresentativeScheme, options => Cookie(options, "sd.representative", "/api/representative", builder.Environment));
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Employee", policy => policy.AddAuthenticationSchemes(EmployeeScheme).RequireAuthenticatedUser().RequireRole("employee").RequireAssertion(ctx => DemoEnabled(builder.Configuration) || !ctx.User.HasClaim("auth_source", "demo")));
            options.AddPolicy("Representative", policy => policy.AddAuthenticationSchemes(RepresentativeScheme).RequireAuthenticatedUser().RequireRole("representative").RequireAssertion(ctx => DemoEnabled(builder.Configuration) || !ctx.User.HasClaim("auth_source", "demo")));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("AdvisorEnrollment", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("AdvisorTurns", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
    private static void Cookie(CookieAuthenticationOptions options, string name, string path, IWebHostEnvironment environment)
    {
        options.Cookie.Name = name; options.Cookie.Path = path; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return ctx.Response.WriteAsJsonAsync(new { title = "יש להיכנס לתפקיד המתאים", status = 401 }); };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return ctx.Response.WriteAsJsonAsync(new { title = "אין הרשאה לתפקיד זה", status = 403 }); };
        options.Events.OnValidatePrincipal = ctx =>
        {
            var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id is null || ctx.HttpContext.RequestServices.GetRequiredService<AdvisorStore>().Session(id) is null)
                ctx.RejectPrincipal();
            return Task.CompletedTask;
        };
    }
    private static bool DemoEnabled(IConfiguration c) => c.GetValue("Advisor:DemoAuthEnabled", false);
    private static string Owner(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new AdvisorException(401, "יש להתחבר מחדש.");
    private static async Task SignIn(HttpContext ctx, string scheme, EmployeeSession session, string role)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, session.Id), new Claim(ClaimTypes.Role, role), new Claim("auth_source", "demo") };
        await ctx.SignInAsync(scheme, new ClaimsPrincipal(new ClaimsIdentity(claims, scheme)), new AuthenticationProperties { IsPersistent = true });
    }
    public static void UseAdvisor(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() && DemoEnabled(app.Configuration)
            && string.IsNullOrWhiteSpace(app.Configuration["Advisor:DemoRepresentativeCode"]))
            throw new InvalidOperationException("Production demo enrollment requires Advisor:DemoRepresentativeCode. Disable demo enrollment for real identity integration.");
        app.Use(async (ctx, next) =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/api/advisor") && !ctx.Request.Path.StartsWithSegments("/api/representative")) { await next(); return; }
            try
            {
                if (!HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method))
                {
                    if (ctx.Request.Headers["X-Strauss-Client"] != "employee-web") throw new AdvisorException(403, "כותרת אבטחת הבקשה חסרה.");
                    if (ctx.Request.ContentLength > 65536) throw new AdvisorException(413, "הבקשה גדולה מדי.");
                    var size = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>(); if (size is { IsReadOnly: false }) size.MaxRequestBodySize = 65536;
                }
                ctx.Response.Headers.CacheControl = "no-store";
                await next();
            }
            catch (AdvisorException ex) { await Results.Problem(statusCode: ex.Status, title: ex.Message, detail: ex.Message).ExecuteAsync(ctx); }
            catch (CalculationException) { await Results.Problem(statusCode: 422, title: "נתוני המסלול חסרים או אינם תקינים.").ExecuteAsync(ctx); }
        });
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
        app.MapGet("/api/advisor/mode", (ConversationAgent agent, IConfiguration c, EvaluationBudget budget) => Results.Ok(new { agentMode = agent.Mode, demoAuthentication = DemoEnabled(c), evaluationBudget = budget.Snapshot() }));
        app.MapPost("/api/advisor/sign-out", async (HttpContext ctx, AdvisorStore store) =>
        {
            var identities = new List<string> { Owner(ctx) };
            var remembered = ctx.Request.Cookies.Keys.Where(n => n.StartsWith("sd.profile.", StringComparison.Ordinal)).ToArray();
            var protection = ctx.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("StraussDrive.DemoProfileRecovery.v1");
            foreach (var name in remembered)
            {
                // Logout ends every remembered employee profile in this browser.
                try
                {
                    identities.Add(protection.Unprotect(ctx.Request.Cookies[name]!));
                }
                catch (System.Security.Cryptography.CryptographicException) { }
            }
            store.RevokeSessions(identities);
            await ctx.SignOutAsync(EmployeeScheme);
            foreach (var name in remembered) ctx.Response.Cookies.Delete(name, new CookieOptions { Path = "/api/advisor" });
            return Results.Ok(new { signedOut = true });
        }).RequireAuthorization("Employee");
        app.MapPost("/api/representative/sign-out", async (HttpContext ctx, AdvisorStore store) =>
        {
            store.RevokeSession(Owner(ctx));
            await ctx.SignOutAsync(RepresentativeScheme);
            return Results.Ok(new { signedOut = true });
        }).RequireAuthorization("Representative");
        app.MapPost("/api/advisor/employee-session", async (SessionRequest request, HttpContext ctx, AdvisorStore store, HackathonData data, ConversationAgent agent, IConfiguration c, IDataProtectionProvider protection) =>
        {
            if (!DemoEnabled(c)) throw new AdvisorException(403, "כניסת הדגמה כבויה. יש להגדיר אימות אמיתי לפני שימוש.");
            var profile = request.SampleEmployeeId ?? "custom";
            if (profile != "custom" && !data.Employees.ContainsKey(profile)) throw new AdvisorException(422, "עובד הדגמה לא נמצא.");
            var auth = await ctx.AuthenticateAsync(EmployeeScheme); var id = auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = id is null ? null : store.Session(id);
            var recover = protection.CreateProtector("StraussDrive.DemoProfileRecovery.v1");
            var cookieName = "sd.profile." + profile;
            if (existing?.ProfileKey != profile && ctx.Request.Cookies.TryGetValue(cookieName, out var protectedId))
            {
                try { var restored = store.Session(recover.Unprotect(protectedId)); if (restored?.ProfileKey == profile) existing = restored; }
                catch (System.Security.Cryptography.CryptographicException) { /* Invalid recovery cookies do not grant an identity. */ }
            }
            var session = existing?.ProfileKey == profile ? existing : store.CreateSession(profile);
            await SignIn(ctx, EmployeeScheme, session, "employee");
            ctx.Response.Cookies.Append(cookieName, recover.Protect(session.Id), new CookieOptions { Path = "/api/advisor", HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = !app.Environment.IsDevelopment() || ctx.Request.IsHttps, MaxAge = TimeSpan.FromDays(7), IsEssential = true });
            return Results.Ok(new SessionView(session.Id, "employee", profile, true, agent.Mode));
        }).RequireRateLimiting("AdvisorEnrollment");
        // Status only, still behind DemoGate. Authenticate without challenging: a normal
        // missing/expired role cookie must not evict the browser's cached Basic credentials.
        // Ticket routes continue to require the Representative policy and return 401/403.
        app.MapGet("/api/representative/session", async (HttpContext ctx, IAuthorizationService authorization) =>
        {
            var auth = await ctx.AuthenticateAsync(RepresentativeScheme);
            var authenticated = auth.Succeeded && auth.Principal is not null
                && (await authorization.AuthorizeAsync(auth.Principal, null, "Representative")).Succeeded;
            return Results.Ok(new { authenticated });
        });
        app.MapPost("/api/representative/session", async (RepresentativeLoginRequest request, HttpContext ctx, AdvisorStore store, IConfiguration c) =>
        {
            if (!DemoEnabled(c)) throw new AdvisorException(403, "כניסת נציג הדגמה כבויה.");
            var code = c["Advisor:DemoRepresentativeCode"];
            if (!string.IsNullOrEmpty(code) && !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(code), System.Text.Encoding.UTF8.GetBytes(request.Code ?? "")))
                throw new AdvisorException(403, "קוד נציג אינו תקין.");
            var auth = await ctx.AuthenticateAsync(RepresentativeScheme); var id = auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = id is null ? null : store.Session(id); session ??= store.CreateSession("representative");
            await SignIn(ctx, RepresentativeScheme, session, "representative");
            return Results.Ok(new SessionView(session.Id, "representative", "representative", true, "not-applicable"));
        }).RequireRateLimiting("AdvisorEnrollment");
        app.MapGet("/api/advisor/options", (HackathonData data) => Results.Ok(new
        {
            occupations = data.Occupations.Values.Select(o => new { o.Name, o.Group, o.MandatoryCar, o.CanWaive }),
            vehicles = data.Catalog.Values.Select(v => new { v.Id, name = v.CommercialName, v.Drive, v.Group }),
            payrollTaxYear = data.Rules.PayrollTaxYear,
            childMaxAge = data.Rules.Policy.Family.ChildMaxAge
        }));
        app.MapGet("/api/advisor/sources/rules", (HackathonData data) => Results.Ok(data.RawRules));
        app.MapGet("/api/advisor/sources/vehicles/{id}", (string id, HackathonData data) => data.Catalog.TryGetValue(id, out var car)
            ? Results.Ok(new { car.Id, car.Key, car.CommercialName, car.TechnicalDescription, valueOfUse = data.VehicleValues[car.Key].ValueOfUse, source = data.Sources.Single(s => s.FileName == "portfolio-demo.json") })
            : Results.NotFound());
        var employee = app.MapGroup("/api/advisor/conversations").RequireAuthorization("Employee");
        employee.MapGet("/", (HttpContext ctx, AdvisorStore store) => Results.Ok(store.List(Owner(ctx)).Select(c => new { c.Id, c.Title, c.ProfileKey, c.State.Stage, c.UpdatedAt, c.Version })));
        employee.MapPost("/", (HttpContext ctx, AdvisorStore store, ConversationAgent agent) =>
        {
            var session = store.Session(Owner(ctx)) ?? throw new AdvisorException(401, "יש להתחבר מחדש.");
            if (store.List(session.Id).Length >= 30) throw new AdvisorException(422, "הגעת למגבלת שיחות ההדגמה לחשבון זה.");
            var c = store.Create(session, agent.Initial(session.ProfileKey), agent.Welcome(session.ProfileKey)); return Results.Ok(store.View(c.Id, session.Id));
        }).RequireRateLimiting("AdvisorTurns");
        employee.MapGet("/{id}", (string id, HttpContext ctx, AdvisorStore store) => Results.Ok(store.View(id, Owner(ctx))));
        employee.MapPost("/{id}/messages", async (string id, MessageRequest request, HttpContext ctx, AdvisorStore store, ConversationAgent agent) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 4000) throw new AdvisorException(422, "יש לשלוח הודעה באורך 1–4000 תווים.");
            var c = store.RequireConversation(id, Owner(ctx));
            if (c.Version != request.ExpectedVersion) throw new AdvisorException(409, "השיחה עודכנה; יש לרענן לפני שליחה חוזרת.");
            if (store.View(id, c.OwnerId).Messages.Length >= 201) throw new AdvisorException(422, "השיחה הגיעה למגבלת ההדגמה. יש לפתוח שיחה חדשה.");
            var user = ConversationAgent.Message("employee", request.Text, "user-input");
            if (c.State.Stage == "AwaitingClarification" && request.ShareWithRepresentative)
            {
                c.State.Stage = "WaitingForRepresentative";
                store.Clarify(c, c.Version, user, ConversationAgent.Message("assistant", "ההבהרה הועברה לנציג בהסכמתך. נמשיך לאחר תשובה."), request.Text);
            }
            else
            {
                var outcome = await agent.Turn(c, request, ctx.RequestAborted); store.Save(c with { Title = agent.Title(c.State) }, c.Version, user, outcome);
            }
            return Results.Ok(store.View(id, c.OwnerId));
        }).RequireRateLimiting("AdvisorTurns");
        var representative = app.MapGroup("/api/representative/tickets").RequireAuthorization("Representative");
        representative.MapGet("/", (AdvisorStore store) => Results.Ok(store.Tickets()));
        representative.MapGet("/{id}", (string id, AdvisorStore store) => Results.Ok(store.TicketView(id)));
        representative.MapPost("/{id}/actions", (string id, RepresentativeAction request, HttpContext ctx, AdvisorStore store, ConversationAgent agent) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000 || request.Action is not ("answer" or "clarification" or "close")) throw new AdvisorException(422, "יש לבחור פעולה תקינה ולהזין תשובה באורך עד 2000 תווים.");
            var ticket = store.Ticket(id); var c = store.ConversationForTicket(ticket);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Jerusalem").DateTime);
            var effectiveFrom = request.Action == "answer" && ticket.Category == "Participation" ? request.EffectiveFrom ?? today : (DateOnly?)null;
            if (effectiveFrom is { } date && (date > today || date.Year < 1900))
                throw new AdvisorException(422, "תאריך התחולה חייב להיות היום או בעבר. המחשבון חודשי ואינו מחיל אישורים עתידיים או מחשב הפרשים רטרואקטיביים.");
            if (c.State.PendingTicketId != id) throw new AdvisorException(409, "המסלול בשיחה השתנה; אין להחיל תשובה על שלב אחר.");
            ConversationMessage message;
            if (request.Action == "clarification")
            {
                c.State.Stage = "AwaitingClarification"; ticket = ticket with { Status = "ClarificationRequested" };
                message = ConversationAgent.Message("representative", "הנציג מבקש הבהרה: " + request.Text, "human");
            }
            else if (request.Action == "close")
            {
                c.State.ProposedHandoff = null; c.State.PendingCapability = null;
                c.State.PendingTicketId = null; c.State.ConfirmedParticipation = null; c.State.ConfirmedVehicle = null; c.State.Stage = "SelectingAlternative"; ticket = ticket with { Status = "Closed" };
                message = ConversationAgent.Message("representative", "הפנייה נסגרה ללא אישור עסקי: " + request.Text + ". אפשר להמשיך לפי נתוני המקור או לבחור חלופה אחרת.", "human");
            }
            else
            {
                c.State.ProposedHandoff = null; c.State.PendingCapability = null;
                if (ticket.Category == "Participation")
                {
                    if (request.ParticipationAmount is null or < 0 or > 1000000) throw new AdvisorException(422, "נדרש סכום השתתפות בסיסי מאושר, לא שלילי.");
                    if (ticket.Target == "current")
                    {
                        if (ticket.VehicleId != c.State.Facts.CurrentCar) throw new AdvisorException(409, "המסלול הנוכחי השתנה.");
                        c.State.Facts = c.State.Facts with { CurrentParticipation = request.ParticipationAmount };
                        c.State.Evidence[nameof(EmployeeFacts.CurrentParticipation)] = new("representative_approval", "usable");
                    }
                    else
                    {
                        if (ticket.VehicleId != c.State.Alternative) throw new AdvisorException(409, "הרכב השתנה מאז פתיחת הפנייה.");
                        c.State.ConfirmedParticipation = request.ParticipationAmount; c.State.ConfirmedVehicle = ticket.VehicleId;
                        c.State.Evidence["AlternativeParticipation"] = new("representative_approval", "usable");
                    }
                }
                c.State.PendingTicketId = null; ticket = ticket with { Status = "Answered" };
                var resumed = agent.Continue(c, "human-confirmed").Message;
                message = resumed with { Role = "representative", Text = "תשובת נציג: " + request.Text
                    + (effectiveFrom is { } start ? $" · בתוקף מ־{start:yyyy-MM-dd}. החישוב הוא אומדן חודשי, ללא הפרשים רטרואקטיביים." : "") + "\n\n" + resumed.Text };
            }
            var approvedAmount = request.Action == "answer" && ticket.Category == "Participation" ? request.ParticipationAmount : null;
            store.RepresentativeUpdate(ticket, request.ExpectedVersion, c, c.Version, message, Owner(ctx), request.Action, request.Text, approvedAmount, effectiveFrom);
            return Results.Ok(store.TicketView(id));
        }).RequireRateLimiting("AdvisorTurns");
    }
}
