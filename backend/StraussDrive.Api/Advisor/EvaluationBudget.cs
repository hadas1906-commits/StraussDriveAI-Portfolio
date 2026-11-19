namespace StraussDrive.Api.Advisor;

// Opt-in guard for one dedicated evaluation process. Never a substitute for provider billing controls.
public sealed class EvaluationBudget
{
    private readonly object gate = new();
    public bool Enabled { get; }
    public decimal MaxCostUsd { get; }
    public decimal InputUsdPerMillion { get; }
    public decimal OutputUsdPerMillion { get; }
    public int MaxRequests { get; }
    private int reservedRequests;
    private decimal reservedCost;
    private bool stopped;
    public EvaluationBudget(IConfiguration config)
    {
        Enabled = config.GetValue("Advisor:Evaluation:Enabled", false);
        if (!Enabled) return;
        MaxCostUsd = config.GetValue<decimal>("Advisor:Evaluation:MaxCostUsd");
        InputUsdPerMillion = config.GetValue<decimal>("Advisor:Evaluation:InputUsdPerMillion");
        OutputUsdPerMillion = config.GetValue<decimal>("Advisor:Evaluation:OutputUsdPerMillion");
        MaxRequests = config.GetValue<int>("Advisor:Evaluation:MaxRequests");
        if (MaxCostUsd is <= 0 or > 100 || InputUsdPerMillion is <= 0 or > 10000
            || OutputUsdPerMillion is <= 0 or > 10000 || MaxRequests is < 1 or > 500)
            throw new InvalidOperationException("Evaluation requires positive reviewed prices, a USD limit up to 100 and 1..500 requests.");
    }
    public object Snapshot() { lock (gate) return new { enabled = Enabled, maxCostUsd = MaxCostUsd,
        inputUsdPerMillion = InputUsdPerMillion, outputUsdPerMillion = OutputUsdPerMillion, maxRequests = MaxRequests,
        reservedRequests, reservedCostUsd = reservedCost, stopped, outputTokenLimit = 1600,
        accounting = "conservative_utf8_bytes_plus_4096_input_and_max_output; no refunds; provider billing authoritative" }; }
    public Permit Reserve(int serializedRequestBytes)
    {
        lock (gate)
        {
            if (!Enabled) return new(this, 0, false);
            // UTF-8 bytes conservatively bound byte-level tokenizers; include protocol overhead.
            var inputBound = checked(serializedRequestBytes + 4096);
            var cost = (inputBound * InputUsdPerMillion + 1600m * OutputUsdPerMillion) / 1000000m;
            if (stopped || reservedRequests >= MaxRequests || reservedCost + cost > MaxCostUsd)
                throw new ProviderFailureException(new("evaluation_budget_exhausted", null, 0, ""));
            reservedRequests++; reservedCost += cost;
            return new(this, inputBound, true);
        }
    }
    public sealed class Permit(EvaluationBudget budget, int inputBound, bool active) : IDisposable
    {
        private bool known;
        public void Observe(int? input, int? output)
        {
            if (!active) return;
            lock (budget.gate)
            {
                known = input is >= 0 && output is >= 0 && input <= inputBound && output <= 1600;
                if (!known) budget.stopped = true;
            }
        }
        public void Dispose() { if (active && !known) lock (budget.gate) budget.stopped = true; }
    }
}
