using MexicanStandoff.Bots;
using MexicanStandoff.Engine;
using MexicanStandoff.Simulation;

var gamesPerCell = ArgValue("--games", 1000);
var masterSeed = ArgValue("--seed", 12345);
var startingBullets = ArgValue("--start-bullets", 0);

var baseline = GameParameters.Default with { StartingBullets = startingBullets };

// Healing sweep (v2 experiment): start at 2 HP, spend gold to heal up to a raised
// ceiling. Sweeps the HP ceiling (3, 4) and the per-heal cost (1, 2 bars) against
// the no-heal baseline. Watch the `heal` column: if bots almost never heal, the
// card is dead; the win-rate spread shows whether it rescues the strong turtle.
GameParameters Heal(int maxHp, int cost, bool refund) =>
    baseline with { HealingEnabled = true, MaxHp = maxHp, HealCost = cost, HealCostRefundedOnCancel = refund };

// Refund policy: does a heal cancelled by a hit refund the gold (Load-like, the
// default) or spend it anyway? Bots only heal when no opponent is armed, so their
// heals are never cancelled — expect the two policies to look near-identical here.
var configs = new (string Name, GameParameters Parameters)[]
{
    ($"baseline no-heal (hp2 maxHp2 startBullets{startingBullets})", baseline),
    ("heal maxHp3 cost2 refund", Heal(maxHp: 3, cost: 2, refund: true)),
    ("heal maxHp3 cost2 NO-refund", Heal(maxHp: 3, cost: 2, refund: false)),
    ("heal maxHp4 cost2 refund", Heal(maxHp: 4, cost: 2, refund: true)),
    ("heal maxHp4 cost2 NO-refund", Heal(maxHp: 4, cost: 2, refund: false)),
};

int[] playerCounts = [2, 3, 4, 5, 6, 8];

IBot[] botPool = [new AdaptiveBot(), new AggressiveBot(), new ChestRusherBot(), new TurtleBot(), new RandomBot()];

Console.WriteLine($"Simulating {configs.Length} configs x {playerCounts.Length} player counts x {gamesPerCell} games "
    + $"(seed {masterSeed}, ~30s/round assumed for duration)\n");

foreach (var (name, parameters) in configs)
{
    Console.WriteLine($"=== {name} ===");
    foreach (var playerCount in playerCounts)
    {
        var stats = new SweepStats();
        var cellSeed = HashCode.Combine(masterSeed, name, playerCount);

        for (var game = 0; game < gamesPerCell; game++)
        {
            var rng = new Random(HashCode.Combine(cellSeed, game));
            // Cycle strategies through the seats, then shuffle seat order.
            var bots = Enumerable.Range(0, playerCount).Select(i => botPool[i % botPool.Length]).ToArray();
            rng.Shuffle(bots);

            var outcome = GameRunner.Play(parameters, bots, seed: rng.Next());
            stats.Add(outcome, bots.Select(b => b.StrategyName));
        }

        var reasons = string.Join(" ", stats.ReasonShares.Select(r => $"{Label(r.Reason)}:{r.Share:P0}"));
        var winRates = string.Join(" ", stats.WinRates.Select(w => $"{w.Strategy}:{w.WinRate:P0}"));
        Console.WriteLine(
            $"  {playerCount}p | rounds avg {stats.AvgRounds,5:F1} p50 {stats.Percentile(50),3} p90 {stats.Percentile(90),3} "
            + $"| ~{stats.AvgRounds * 0.5,4:F1} min | timeout {stats.TimeoutRate:P1} "
            + $"| heal {stats.HealRate:P0}/{stats.AvgHeals:F1} "
            + $"| {reasons} | {winRates}");
    }

    Console.WriteLine();
}

return;

static string Label(WinReason reason) => reason switch
{
    WinReason.GoldTarget => "gold",
    WinReason.LastStanding => "last",
    WinReason.MutualDestruction => "mutual",
    _ => reason.ToString(),
};

int ArgValue(string flag, int fallback)
{
    var index = Array.IndexOf(args, flag);
    return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value)
        ? value
        : fallback;
}
