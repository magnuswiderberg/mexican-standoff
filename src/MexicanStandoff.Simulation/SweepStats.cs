using MexicanStandoff.Engine;

namespace MexicanStandoff.Simulation;

/// <summary>Aggregated outcomes for one (parameter config, player count) cell.</summary>
public sealed class SweepStats
{
    private readonly List<int> _rounds = [];
    private readonly Dictionary<string, int> _winsByStrategy = [];
    private readonly Dictionary<string, int> _seatsByStrategy = [];
    private readonly Dictionary<WinReason, int> _reasons = [];

    public int Games { get; private set; }
    public int Timeouts { get; private set; }

    public void Add(GameOutcome outcome, IEnumerable<string> seatedStrategies)
    {
        Games++;
        _rounds.Add(outcome.Rounds);

        foreach (var strategy in seatedStrategies)
            _seatsByStrategy[strategy] = _seatsByStrategy.GetValueOrDefault(strategy) + 1;

        if (outcome.TimedOut)
        {
            Timeouts++;
            return;
        }

        _reasons[outcome.Reason!.Value] = _reasons.GetValueOrDefault(outcome.Reason.Value) + 1;
        foreach (var winner in outcome.WinnerStrategies)
            _winsByStrategy[winner] = _winsByStrategy.GetValueOrDefault(winner) + 1;
    }

    public double AvgRounds => _rounds.Average();
    public int Percentile(int p)
    {
        var sorted = _rounds.OrderBy(r => r).ToList();
        return sorted[Math.Min(sorted.Count - 1, sorted.Count * p / 100)];
    }

    public double TimeoutRate => (double)Timeouts / Games;

    public IEnumerable<(WinReason Reason, double Share)> ReasonShares =>
        _reasons.OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, (double)kv.Value / (Games - Timeouts)));

    public IEnumerable<(string Strategy, double WinRate)> WinRates =>
        _seatsByStrategy
            .Select(kv => (kv.Key, _winsByStrategy.GetValueOrDefault(kv.Key) / (double)kv.Value))
            .OrderByDescending(t => t.Item2);
}
