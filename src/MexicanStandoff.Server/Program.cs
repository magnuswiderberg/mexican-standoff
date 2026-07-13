using MexicanStandoff.Engine;
using MexicanStandoff.Server.Contracts;
using MexicanStandoff.Server.Games;
using MexicanStandoff.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.Configure<GameOptions>(builder.Configuration.GetSection("Game"));
builder.Services.AddSingleton<IGameStore, InMemoryGameStore>();
builder.Services.AddSingleton<GameService>();

var app = builder.Build();

// The built React SPA (vite build emits into wwwroot). In dev the Vite server
// serves the SPA instead and proxies /hub here.
//
// Party wifi is the bottleneck: eight phones pull the same portraits, fonts and
// bundles at the same moment, and a mid-game reload must not pay for them twice.
// index.html is the deliberate exception — a shell cached across a deploy would
// speak last week's contracts to this week's hub.
var staticFiles = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = ctx.File.Name switch
        {
            "index.html" => "no-cache",
            // Vite fingerprints everything under /assets — a changed file gets a changed name.
            _ when ctx.Context.Request.Path.StartsWithSegments("/assets") => "public,max-age=31536000,immutable",
            // Unhashed public/ files: long enough to outlive a party, short enough that new art lands the next day.
            _ => "public,max-age=86400",
        },
};

app.UseDefaultFiles();
app.UseStaticFiles(staticFiles);

app.MapHub<GameHub>("/hub/game");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Rule numbers for the "How to play" page (games always run the engine defaults).
var rules = GameParameters.Default;
app.MapGet(
    "/api/rules",
    () => Results.Ok(new RulesView(
        rules.StartingHp, rules.MaxBullets, rules.GoldToWin, rules.GoldPerChest, rules.DuelSequenceLength)));

// Client-side routes (/game/CODE, /monitor/CODE) all serve the SPA shell.
app.MapFallbackToFile("index.html", staticFiles);

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
