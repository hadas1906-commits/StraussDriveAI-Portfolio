using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StraussDrive.Api.Advisor;
using StraussDrive.Domain;
using StraussDrive.Import;
using Xunit;

namespace StraussDrive.Tests;
public static class PortfolioTestData
{
    public static readonly HackathonData Data = HackathonImporter.Load(HackathonImporter.FindRoot(AppContext.BaseDirectory));
}
public sealed class PortfolioTests
{
    private static HackathonData Data => PortfolioTestData.Data;
    [Theory]
    [InlineData("DEMO-P01",9945,10138,193)]
    [InlineData("DEMO-P02",13074,12085,-989)]
    [InlineData("DEMO-P03",16778,18470,1692)]
    [InlineData("DEMO-P04",11152,11203,51)]
    [InlineData("DEMO-P05",10591,9555,-1036)]
    public void IndependentFictionalFinancialReferences(string id,int current,int alternative,int difference)
    {
        var result=Data.CompareReference(id);
        Assert.Equal(current,result.Current.Salary.DisplayNetSalary);
        Assert.Equal(alternative,result.Alternative.Salary.DisplayNetSalary);
        Assert.Equal(difference,result.DisplayNetDifference);
        Assert.All(Data.ValidateReferences().Observations.Where(x=>x.EmployeeId==id),x=>Assert.True(x.Matches,x.Field));
    }
    [Theory]
    [InlineData("DEMO-P01",false)] [InlineData("DEMO-P02",true)]
    [InlineData("DEMO-P03",false)] [InlineData("DEMO-P04",false)] [InlineData("DEMO-P05",false)]
    public void FamilyEligibilityAndOriginalFortyPercentSplit(string id,bool expected)
    {
        var e=Data.Employees[id];var result=Data.Advisor.CheckEligibility(e,"DEMO-106");
        Assert.Equal(expected,result.Eligible);
        if(expected){Assert.Equal(800,result.FamilyEmployeeShare);Assert.Equal(1200,result.FamilyEmployerShare);}
    }
    [Theory]
    [InlineData("DEMO-P01",true)] [InlineData("DEMO-P02",false)] [InlineData("DEMO-P03",true)]
    public void WaiverIsOccupationControlled(string id,bool expected)=>Assert.Equal(expected,Data.Advisor.CheckEligibility(Data.Employees[id],null).Eligible);
    [Fact] public void HigherGroupDoesNotBecomeEligibleBecauseBudgetIsKnown()=>Assert.False(Data.Advisor.CheckEligibility(Data.Employees["DEMO-P01"],"DEMO-105").Eligible);
    [Fact] public void ClaimedGroupCannotAlterOccupation()=>Assert.Throws<CalculationException>(()=>Data.Advisor.CheckEligibility(Data.Employees["DEMO-P01"] with{Group="ג"},"DEMO-105"));
    [Fact] public void ElectricGrossUpProducesExactlyOriginalNetTarget()
    {
        var r=Data.Advisor.Calculate(Data.Employees["DEMO-P01"],"DEMO-102");
        var without=new SalaryCalculator(Data.Rules.Tax,Data.Rules.Business).Calculate(r.Scenario with{IncludeElectricReimbursement=false});
        Assert.InRange(r.Salary.NetSalary-without.NetSalary,174.999999m,175.000001m);
    }
    [Theory]
    [InlineData("PULSE","DEMO-102")] [InlineData("פולס","DEMO-102")] [InlineData("DEMO-106","DEMO-106")]
    public void FictionalAliasesAndExactIdentifiers(string query,string id)=>Assert.Equal(id,Assert.Single(VehicleSearch.Lookup(Data.Catalog.Values,query)).Id);
    [Fact] public void UnknownIdentifierCannotPartiallyMatch()=>Assert.Empty(VehicleSearch.Lookup(Data.Catalog.Values,"DEMO-10"));
    private sealed class Scripted(TurnPlan plan):IConversationProvider
    {
        public string Mode=>"scripted-local";public bool UsesNativeCapabilities=>true;
        public Task<TurnPlan> PlanAsync(string text,ConversationState state,CancellationToken token)=>Task.FromResult(plan);
    }
    private static AdvisorCommand Command(string name,object args)=>new(name,JsonSerializer.SerializeToElement(args));
    [Fact] public async Task NativePlanCompletesGoalAndDeduplicatesOriginalFamilyCalculation()
    {
        var calculation=Command("calculate_vehicle_comparison",new{vehicles=new[]{"DEMO-106"}});
        var plan=new TurnPlan("tools"){Goal=new("comparison",calculation),Commands=[Command("get_active_employee",new{}),calculation,calculation]};
        var agent=new ConversationAgent(Data,new AdvisorTools(Data),new Scripted(plan),new(Data));
        var chat=new Conversation("local","owner","DEMO-P02","fictional",agent.Initial("DEMO-P02"),0,"now","now");
        var facts=chat.State.Facts;
        var result=await agent.Turn(chat,new("השוואה משפחתית",0),default);
        Assert.True(result.Message.Comparison is not null, result.Message.Text);
        var comparison=result.Message.Comparison!;
        Assert.Single(result.Message.Tools, t => t.Name == "calculate_vehicle_comparison");
        Assert.Equal(12085,comparison.Alternative.Salary.DisplayNetSalary);Assert.Equal(facts,chat.State.Facts);
        Assert.Equal(800,comparison.Alternative.FamilyEmployeeShare);
    }
    [Theory]
    [InlineData("employeeId")] [InlineData("approvedAmount")] [InlineData("taxRate")]
    public void ToolPlanCannotIntroduceAuthority(string field)=>Assert.Throws<InvalidOperationException>(()=>AdvisorCapabilities.Validate(Command("calculate_vehicle_comparison",new Dictionary<string,object>{{"vehicles",new[]{"DEMO-102"}},{field,"forbidden"}})));
    [Fact] public async Task RealDeterministicDemoAndFollowupKeepKnownProfile()
    {
        var demo=new DemoConversationProvider(Data);var agent=new ConversationAgent(Data,new AdvisorTools(Data),demo,demo);
        var chat=new Conversation("demo","owner","DEMO-P02","fictional",agent.Initial("DEMO-P02"),0,"now","now");
        var result=await agent.Turn(chat,new("השוואה משפחתית מול HARBOR",0),default);
        Assert.Equal("deterministic-demo",result.Message.Mode);Assert.NotNull(result.Message.Comparison);
        Assert.Equal(12085,result.Message.Comparison!.Alternative.Salary.DisplayNetSalary);
        var followup=await agent.Turn(chat,new("מה חלקי בפער התקציב?",0),default);
        Assert.Contains("800",followup.Message.Text);Assert.Null(chat.State.PendingField);
    }
    [Fact] public async Task DeterministicHandoffRequiresSeparateConfirmation()
    {
        var demo=new DemoConversationProvider(Data);var agent=new ConversationAgent(Data,new AdvisorTools(Data),demo,demo);
        var chat=new Conversation("handoff","owner","DEMO-P02","fictional",agent.Initial("DEMO-P02"),0,"now","now");
        await agent.Turn(chat,new("השוואה משפחתית מול HARBOR",0),default);
        var proposed=await agent.Turn(chat,new("נציג לאישור השתתפות",0),default);
        Assert.Null(proposed.NewTicket);Assert.Null(chat.State.PendingTicketId);Assert.NotNull(chat.State.ProposedHandoff);
        var confirmed=await agent.Turn(chat,new("אישור העברה לנציג",0,ConfirmHandoff:chat.State.ProposedHandoff!.Id),default);
        Assert.NotNull(confirmed.NewTicket);Assert.NotNull(chat.State.PendingTicketId);
    }
    [Fact] public void SourceContainsNoGovernmentOrChallengeArtifacts()
    {
        Assert.Equal("portfolio-demo.json",Assert.Single(Data.Sources).FileName);
        Assert.Equal(2099,Data.Rules.PayrollTaxYear);Assert.Equal(6,Data.Catalog.Count);Assert.Equal(5,Data.Employees.Count);
    }
}
public sealed class PortfolioApiTests:IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"portfolio-tests-"+Guid.NewGuid());
    private readonly WebApplicationFactory<Program> factory;
    public PortfolioApiTests()
    {
        factory=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
            b.UseEnvironment("Development");
            b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{
                ["Advisor:DatabasePath"]=Path.Combine(directory,"demo.db"),["Advisor:DemoAuthEnabled"]="true",
                // Deliberately present unusable external settings: portfolio must still have no networking provider.
                ["Advisor:ForceDeterministic"]="false",["STRAUSS_AI_ENDPOINT"]="https://unreachable.invalid/v1",
                ["STRAUSS_AI_API_KEY"]="synthetic-not-a-real-key",["STRAUSS_AI_MODEL"]="synthetic"}));
            b.ConfigureServices(s=>s.AddSingleton(PortfolioTestData.Data));});
    }
    private HttpClient Client(){var c=factory.CreateClient();c.DefaultRequestHeaders.Add("X-Strauss-Client","employee-web");return c;}
    [Fact] public async Task ExternalSettingsCannotSelectPaidProvider()
    {
        using var c=Client();var r=await c.GetFromJsonAsync<JsonElement>("/api/advisor/mode");
        Assert.Equal("deterministic-demo",r.GetProperty("agentMode").GetString());
    }
    [Fact] public async Task WritesWithoutCsrfHeaderAreRejected()
    {
        using var c=factory.CreateClient();Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync("/api/advisor/employee-session",new{sampleEmployeeId="DEMO-P01"})).StatusCode);
    }
    [Fact] public async Task ConversationsAreOwnedAndLogoutRevokesAccess()
    {
        using var a=Client();using var b=Client();
        (await a.PostAsJsonAsync("/api/advisor/employee-session",new{sampleEmployeeId="DEMO-P01"})).EnsureSuccessStatusCode();
        (await b.PostAsJsonAsync("/api/advisor/employee-session",new{sampleEmployeeId="DEMO-P01"})).EnsureSuccessStatusCode();
        var created=await a.PostAsJsonAsync("/api/advisor/conversations/",new{});created.EnsureSuccessStatusCode();
        var view=await created.Content.ReadFromJsonAsync<JsonElement>();var id=view.GetProperty("conversation").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound,(await b.GetAsync("/api/advisor/conversations/"+id)).StatusCode);
        (await a.PostAsJsonAsync("/api/advisor/sign-out",new{})).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await a.GetAsync("/api/advisor/conversations/"+id)).StatusCode);
    }
    [Fact] public async Task EmployeeCannotReadRepresentativeTickets()
    {
        using var c=Client();(await c.PostAsJsonAsync("/api/advisor/employee-session",new{sampleEmployeeId="DEMO-P01"})).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/representative/tickets")).StatusCode);
    }
    public void Dispose(){factory.Dispose();if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
