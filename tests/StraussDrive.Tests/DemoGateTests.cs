using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StraussDrive.Tests;

// Environment variables are process-wide: keep these tests isolated from all others.
[CollectionDefinition("Demo gate environment", DisableParallelization = true)]
public sealed class DemoGateEnvironmentCollection { }

[Collection("Demo gate environment")]
public sealed class DemoGateTests : IDisposable
{
    private static readonly string[] Names = ["DEMO_GATE_ENABLED", "DEMO_GATE_USERNAME", "DEMO_GATE_PASSWORD"];
    private readonly string?[] previous = Names.Select(Environment.GetEnvironmentVariable).ToArray();
    private readonly string webRoot = Path.Combine(Path.GetTempPath(), "strauss-gate-" + Guid.NewGuid());

    public DemoGateTests()
    {
        Environment.SetEnvironmentVariable(Names[0], "true");
        Environment.SetEnvironmentVariable(Names[1], "test-user");
        Environment.SetEnvironmentVariable(Names[2], "test-password:with-colon");
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "gate-test.txt"), "static content");
    }

    private WebApplicationFactory<Program> Factory(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment).UseWebRoot(webRoot);
            builder.ConfigureServices(services => services.AddSingleton(PortfolioTestData.Data));
        });

    [Theory]
    [InlineData("/")]
    [InlineData("/gate-test.txt")]
    [InlineData("/api/employees")]
    [InlineData("/api/advisor/employee-session")]
    [InlineData("/api/health/extra")]
    [InlineData("/api/health/")]
    public async Task MissingCredentialsChallengeBeforeRoutesAndFiles(string path)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Basic", response.Headers.WwwAuthenticate.Single().Scheme);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("test-user", "wrong")]
    [InlineData("wrong", "test-password:with-colon")]
    public async Task WrongCredentialsAreRejected(string username, string password)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(username, password);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/employees")).StatusCode);
    }

    [Theory]
    [InlineData("Basic !!!")]
    [InlineData("Basic bm9jb2xvbg==")]
    [InlineData("Bearer token")]
    public async Task MalformedCredentialsAreRejected(string authorization)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/employees")).StatusCode);
    }

    [Fact]
    public async Task CorrectCredentialsAllowApiAndStaticFiles()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("test-user", "test-password:with-colon");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
        Assert.Equal("static content", await client.GetStringAsync("/gate-test.txt"));
    }

    [Fact]
    public async Task HealthIsAccessibleWithoutCredentials()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task GateIsOptIn(string? enabled)
    {
        Environment.SetEnvironmentVariable(Names[0], enabled);
        await using var factory = Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
    }

    [Theory]
    [InlineData("DEMO_GATE_USERNAME", null)]
    [InlineData("DEMO_GATE_PASSWORD", " ")]
    public void ProductionRefusesStartupWithMissingCredentials(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        using var factory = Factory("Production");
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("required credentials are missing", exception.Message);
    }

    public void Dispose()
    {
        for (var i = 0; i < Names.Length; i++) Environment.SetEnvironmentVariable(Names[i], previous[i]);
        Directory.Delete(webRoot, true);
    }

    private static AuthenticationHeaderValue Basic(string username, string password) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
}
