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
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<GameHub>("/hub/game");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Client-side routes (/game/CODE, /monitor/CODE) all serve the SPA shell.
app.MapFallbackToFile("index.html");

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
