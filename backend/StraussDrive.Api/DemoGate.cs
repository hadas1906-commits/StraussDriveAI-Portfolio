using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace StraussDrive.Api;

public static class DemoGate
{
    public static void UseDemoGate(this WebApplication app)
    {
        // Read only process environment variables, never appsettings or secret files.
        if (!string.Equals(Environment.GetEnvironmentVariable("DEMO_GATE_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
            return;

        var username = Environment.GetEnvironmentVariable("DEMO_GATE_USERNAME");
        var password = Environment.GetEnvironmentVariable("DEMO_GATE_PASSWORD");
        var configured = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
        if (!configured && app.Environment.IsProduction())
            throw new InvalidOperationException("Demo gate is enabled but required credentials are missing.");

        var expectedUsername = Hash(username ?? "");
        var expectedPassword = Hash(password ?? "");
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals(new PathString("/api/health")))
            {
                await next();
                return;
            }

            var authorized = false;
            if (configured && AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var header)
                && string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
                && header.Parameter is not null)
            {
                try
                {
                    var decoded = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(header.Parameter));
                    var separator = decoded.IndexOf(':');
                    if (separator >= 0)
                    {
                        // Hash to fixed lengths, and always compare both credentials.
                        authorized = CryptographicOperations.FixedTimeEquals(Hash(decoded[..separator]), expectedUsername)
                            & CryptographicOperations.FixedTimeEquals(Hash(decoded[(separator + 1)..]), expectedPassword);
                    }
                }
                catch (FormatException) { }
                catch (DecoderFallbackException) { }
            }

            if (authorized) await next();
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"StraussDriveAI demo\", charset=\"UTF-8\"";
                context.Response.Headers.CacheControl = "no-store";
            }
        });
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
