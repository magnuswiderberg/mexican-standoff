using MexicanStandoff.Server.Games;
using MexicanStandoff.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.Configure<GameOptions>(builder.Configuration.GetSection("Game"));
builder.Services.AddSingleton<IGameStore, InMemoryGameStore>();
builder.Services.AddSingleton<GameService>();

var app = builder.Build();

app.MapHub<GameHub>("/hub/game");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
