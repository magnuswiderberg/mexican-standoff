using MexicanStandoff.Bots;
using MexicanStandoff.Engine;
using MexicanStandoff.Simulation;

var gamesPerCell = ArgValue("--games", 1000);
var masterSeed = ArgValue("--seed", 12345);
var startingBullets = ArgValue("--start-bullets", 0);

var baseline = GameParameters.Default with { StartingBullets = startingBullets };
var configs = new (string Name, GameParameters Parameters)[]
{
    ($"baseline (hp2 bullets2 chest2 win6 startBullets{startingBullets})", baseline),
    // Grabs-to-win variants (baseline is 3 grabs).
    ("win=4 (2 grabs)", baseline with { GoldToWin = 4 }),
    ("win=8 (4 grabs)", baseline with { GoldToWin = 8 }),
    // Economy scalings: same 3 grabs, coarser/finer loot splits.
    ("legacy chest=1 win=3", baseline with { GoldPerChest = 1, GoldToWin = 3 }),
    ("chest=3 win=9", baseline with { GoldPerChest = 3, GoldToWin = 9 }),
    ("hp=3", baseline with { StartingHp = 3 }),
    ("bullets=3", baseline with { MaxBullets = 3 }),
    ("2 chests from 4 players", baseline with { TwoChestsFromPlayers = 4 }),
    ("single chest always", baseline with { TwoChestsFromPlayers = 99 }),
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
            + $"| lootLost {stats.RoundingLossRate:P0}/{stats.AvgGoldLostShareOfTarget(parameters.GoldToWin):P1} "
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
