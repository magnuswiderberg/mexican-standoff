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
    public int GamesWithRoundingLoss { get; private set; }
    public int TotalGoldLostToRounding { get; private set; }

    public void Add(GameOutcome outcome, IEnumerable<string> seatedStrategies)
    {
        Games++;
        _rounds.Add(outcome.Rounds);
        TotalGoldLostToRounding += outcome.GoldLostToRounding;
        if (outcome.GoldLostToRounding > 0)
            GamesWithRoundingLoss++;

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

    /// <summary>Share of games where the loot split's rounding lost at least one bar.</summary>
    public double RoundingLossRate => (double)GamesWithRoundingLoss / Games;

    /// <summary>Rounding losses as a share of the win target — comparable across rescaled economies.</summary>
    public double AvgGoldLostShareOfTarget(int goldToWin) =>
        (double)TotalGoldLostToRounding / Games / goldToWin;

    public IEnumerable<(WinReason Reason, double Share)> ReasonShares =>
        _reasons.OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, (double)kv.Value / (Games - Timeouts)));

    public IEnumerable<(string Strategy, double WinRate)> WinRates =>
        _seatsByStrategy
            .Select(kv => (kv.Key, _winsByStrategy.GetValueOrDefault(kv.Key) / (double)kv.Value))
            .OrderByDescending(t => t.Item2);
}
