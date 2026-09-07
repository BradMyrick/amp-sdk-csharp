# Amp.Sdk

**AMP SDK for C# — ranked matchmaking, skill ratings, and on-chain settlement for games on Avalanche.**

Works with Unity, Godot, and .NET game servers.

## Install

```
dotnet add package Amp.Sdk
```

## Quickstart

```csharp
using Amp.Sdk;
using Amp.Sdk.Signers;

var amp = new AMPClient(
    "https://amp.playwithamp.xyz",
    new PrivateKeySigner(privateKey)
);

// One gasless signature to log in
await amp.LoginAsync();

// Join a ranked queue
await amp.JoinQueueAsync("amp-tactics", "ranked-1v1");

// Listen for matches
amp.On<MatchFound>("match_found", match =>
{
    Console.WriteLine($"Matched! vs {match.Opponent.Wallet}");
    // Run your game, then report
    await amp.ReportMatchAsync(match.MatchId, "win");
});

// Listen for results
amp.On<JsonElement>("match_result", result =>
{
    Console.WriteLine($"Rating: {result.GetProperty("you").GetProperty("ratingBefore")} → {result.GetProperty("you").GetProperty("ratingAfter")}");
});
```

## What AMP handles vs what you handle

| AMP handles | Your game handles |
|---|---|
| Skill ratings (Glicko-2) | Determining who won |
| Matchmaking queue | Running the actual game |
| Match assignment (WebSocket) | Game UI/UX |
| Result verification | Player experience |
| On-chain escrow + payouts | Your game's economy |
| Anti-collusion | Your game's rules |

## License

Apache-2.0
