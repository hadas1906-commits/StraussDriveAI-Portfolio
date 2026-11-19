using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StraussDrive.Api.Advisor;
using Xunit;

namespace StraussDrive.Tests;

[Collection("Demo gate environment")]
public sealed class RepresentativeGateTests : IDisposable
{
    private static readonly string[] Names = ["DEMO_GATE_ENABLED", "DEMO_GATE_USERNAME", "DEMO_GATE_PASSWORD"];
    private readonly string?[] previous = Names.Select(Environment.GetEnvironmentVariable).ToArray();
    private readonly string directory = Path.Combine(Path.GetTempPath(), "strauss-representative-gate-" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> factory;
    private readonly Clock clock = new();
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    public RepresentativeGateTests()
    {
        Environment.SetEnvironmentVariable(Names[0], "true");
        Environment.SetEnvironmentVariable(Names[1], "synthetic-gate-user");
        Environment.SetEnvironmentVariable(Names[2], "synthetic-gate-password");
        Directory.CreateDirectory(directory);
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Advisor:DemoAuthEnabled"] = "true", ["Advisor:DemoRepresentativeCode"] = "synthetic-role-code",
                ["Advisor:DatabasePath"] = Path.Combine(directory, "advisor.db"), ["Advisor:ForceDeterministic"] = "true"
            }));
            builder.ConfigureServices(services => {
                services.AddSingleton(PortfolioTestData.Data);
                services.Configure<CookieAuthenticationOptions>(AdvisorEndpoints.RepresentativeScheme, options => options.TimeProvider = clock);
            });
        });
    }
    private HttpClient Client(bool basic = true)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Strauss-Client", "employee-web");
        if (basic) client.DefaultRequestHeaders.Authorization = Basic("synthetic-gate-password");
        return client;
    }
    private static AuthenticationHeaderValue Basic(string password) => new("Basic",
        Convert.ToBase64String(Encoding.UTF8.GetBytes("synthetic-gate-user:" + password)));
    private static async Task AssertRoleUnauthorized(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);
        Assert.Null(response.Headers.Location);
        Assert.Equal(401, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
    }
    private static async Task<bool> Status(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/representative/session")).GetProperty("authenticated").GetBoolean();

    [Theory]
    [InlineData(false, "/api/representative/tickets/")]
    [InlineData(true, "/api/representative/tickets/")]
    [InlineData(false, "/api/representative/session")]
    [InlineData(true, "/api/representative/session")]
    public async Task MissingOrWrongBasicCredentialsStillChallengeBeforeApplicationRoutes(bool wrong, string path)
    {
        using var client = Client(false);
        if (wrong) client.DefaultRequestHeaders.Authorization = Basic("incorrect");
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Basic", response.Headers.WwwAuthenticate.Single().Scheme);
    }
    [Fact]
    public async Task ValidGateWithoutRoleGetsSafeStatusButProtectedTicketsStillReturn401WithoutBasicChallenge()
    {
        using var client = Client();
        var response = await client.GetAsync("/api/representative/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(body.EnumerateObject()); Assert.False(body.GetProperty("authenticated").GetBoolean());
        await AssertRoleUnauthorized(await client.GetAsync("/api/representative/tickets/"));
    }
    [Fact]
    public async Task RepresentativeCodeSignsSecureRoleCookieAndRejectsInvalidCodeWithoutElevatingEmployees()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/advisor/employee-session", new SessionRequest("DEMO-P01"))).StatusCode);
        Assert.False(await Status(client));
        await AssertRoleUnauthorized(await client.GetAsync("/api/representative/tickets/"));
        var invalid = await client.PostAsJsonAsync("/api/representative/session", new RepresentativeLoginRequest("incorrect"));
        Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode); Assert.Empty(invalid.Headers.WwwAuthenticate);
        Assert.False(invalid.Headers.Contains("Set-Cookie")); Assert.False(await Status(client));
        var login = await client.PostAsJsonAsync("/api/representative/session", new RepresentativeLoginRequest("synthetic-role-code"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single();
        Assert.StartsWith("sd.representative=", cookie);
        Assert.Contains("path=/api/representative", cookie);
        Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie);
        Assert.True(await Status(client));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/representative/tickets/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/advisor/conversations/")).StatusCode);
    }
    [Fact]
    public async Task RepresentativeCookieCannotAuthorizeEmployeeEndpointsEvenWhenReplayedOutsideItsPath()
    {
        using var representative = Client();
        var login = await representative.PostAsJsonAsync("/api/representative/session", new RepresentativeLoginRequest("synthetic-role-code"));
        using var replay = Client();
        replay.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        await AssertRoleUnauthorized(await replay.GetAsync("/api/advisor/conversations/"));
        Assert.Equal(HttpStatusCode.OK, (await representative.GetAsync("/api/representative/tickets/")).StatusCode);
    }
    [Fact]
    public async Task LogoutRevokesReplayedRepresentativeCookieAndPreservesEmployeeRoleAndGate()
    {
        using var client = Client();
        await client.PostAsJsonAsync("/api/advisor/employee-session", new SessionRequest("DEMO-P01"));
        var login = await client.PostAsJsonAsync("/api/representative/session", new RepresentativeLoginRequest("synthetic-role-code"));
        var cookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/representative/sign-out", new { })).StatusCode);
        using var replay = Client(); replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.False(await Status(replay));
        await AssertRoleUnauthorized(await replay.GetAsync("/api/representative/tickets/"));
        Assert.False(await Status(client));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/advisor/conversations/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
    }
    [Fact]
    public async Task ExpiredSignedRoleCookieReturnsFalseStatusWithoutChallengeAndTicketsRemainProtected()
    {
        using var client = Client();
        await client.PostAsJsonAsync("/api/representative/session", new RepresentativeLoginRequest("synthetic-role-code"));
        Assert.True(await Status(client));
        clock.Now += TimeSpan.FromDays(8);
        Assert.False(await Status(client));
        await AssertRoleUnauthorized(await client.GetAsync("/api/representative/tickets/"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
    }
    public void Dispose()
    {
        factory.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var i = 0; i < Names.Length; i++) Environment.SetEnvironmentVariable(Names[i], previous[i]);
        Directory.Delete(directory, true);
    }
}
