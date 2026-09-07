# Amp.Sdk

**AMP SDK for C# — ranked matchmaking, skill ratings, and on-chain settlement for games on Avalanche.**

Works with Unity, Godot, and .NET game servers.

## Install

```bash
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

// Listen for match assignments (WebSocket)
amp.On<MatchFound>("match_found", async match =>
{
    Console.WriteLine($"⚔️  Matched! vs {match.Opponent.Wallet} ({match.Opponent.Rating} MMR)");

    // Run your game, then report
    var result = await RunMyGame(match);
    await amp.ReportMatchAsync(match.MatchId, result);
});

// Listen for results (rating updates)
amp.On<JsonElement>("match_result", result =>
{
    var you = result.GetProperty("you");
    Console.WriteLine($"Rating: {Math.Round(you.GetProperty("ratingBefore").GetDouble())} → {Math.Round(you.GetProperty("ratingAfter").GetDouble())}");
});
```

## Examples

| Example | What it covers |
|---|---|
| [`QuickStart.cs`](examples/QuickStart.cs) | Full lifecycle: login → queue → match → report → result |
| [`Custodial.cs`](examples/Custodial.cs) | Fiat path: PayPal ↔ AVAX, no crypto needed for players |

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Your Game (Unity, Godot, .NET server)                 │
│  ┌──────────────────────────────────────────────────┐   │
│  │  AMPClient (from Amp.Sdk)                        │   │
│  │  • LoginAsync() — one gasless signature          │   │
│  │  • JoinQueueAsync() — enter the ranked queue     │   │
│  │  • ReportMatchAsync() — report win/loss/draw     │   │
│  │  • On<T>("match_found", ...) — real-time events │   │
│  │  Signer: PrivateKeySigner | IAMPCustodialProvider│   │
│  └──────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────┤
│  REST + WebSocket → https://amp.playwithamp.xyz        │
│  (AMP matchmaker — Rust) → Avalanche Fuji (contracts)  │
└─────────────────────────────────────────────────────────┘
```

### What AMP handles vs what you handle

| AMP handles | Your game handles |
|---|---|
| Skill ratings (Glicko-2) | Determining who won |
| Matchmaking queue + skill windows | Running the actual game |
| Match assignment (WebSocket push) | Game UI/UX |
| Result verification + settlement | Player experience |
| On-chain escrow + payouts | Your game's economy |
| Anti-collusion (commit-reveal) | Your game's rules |

## API Reference

### AMPClient

| Method | Description |
|---|---|
| `LoginAsync()` | Gasless wallet login (one EIP-191 signature) |
| `Logout()` | Clear session |
| `MeAsync()` | Get player info + ratings |
| `GetPlayerAsync(wallet)` | Get any player's public profile |
| `GamesAsync()` | List available games + queue depth |
| `JoinQueueAsync(gameId, rulesetId)` | Join a ranked queue |
| `LeaveQueueAsync()` | Leave the queue |
| `QueueStatusAsync()` | Live queue status |
| `PlayBotAsync()` | Skip the wait, play a bot now |
| `ReportMatchAsync(matchId, result)` | Report a 1v1 result (auto-signs EIP-191) |
| `GetMatchAsync(matchId)` | Get match details |
| `MatchHistoryAsync(limit, offset)` | Recent matches |
| `CreatePartyAsync(gameId, rulesetId)` | Create a party |
| `JoinPartyAsync(inviteCode)` | Join by invite code |
| `GetPartyAsync(partyId)` | Get party details |
| `LockPartyAsync(partyId)` | Lock roster, ready to queue |
| `DisbandPartyAsync(partyId)` | Disband the party |
| `MultiCommitAsync(gameId, stakeWei, lobbySize)` | Commit to FFA queue (returns salt) |
| `MultiRevealAsync(gameId, rulesetId, salt)` | Reveal commit |
| `MultiReportAsync(matchId, ranked, ...)` | Submit N-player ladder (auto-signs EIP-712) |
| `SubmitExitCertAsync(matchId, rank, exitFrame, stateHash)` | Submit a death cert on elimination (auto-signs EIP-191) |
| `CountersignExitCertAsync(matchId, wallet, stateHash)` | Survivor verifies an exit cert |
| `VerifyEscrowAsync(matchId)` | Verify on-chain escrow for staked 1v1 (flips to live) |
| `MultiClaimAsync(matchId)` | Trigger settlement |
| `WaitForMatchAsync(timeout?)` | One call: queue → wait → `MatchFound` (WS + REST fallback) |
| `On<T>(eventType, handler)` | Subscribe to WebSocket event (returns IDisposable) |
| `OnJson(eventType, handler)` | Subscribe with raw JSON (struct-safe) |
| `Disconnect()` | Close WebSocket |

### WebSocket Events

| Event | Payload Type | When |
|---|---|---|
| `hello` | `JsonElement` | On connection |
| `queue_status` | `JsonElement` | Every tick while queued |
| `match_found` | `MatchFound` | Match assigned |
| `match_result` | `JsonElement` | Match settled |
| `match_update` | `JsonElement` | State change (disputed, etc.) |
| `multi_lobby_formed` | `JsonElement` | N-player lobby ready |
| `multi_result` | `JsonElement` | N-player settled |
| `multi_cancelled` | `JsonElement` | N-player cancelled |

### Signer Interfaces

```csharp
// Self-custody (player's wallet or server key)
public interface IAMPSigner
{
    Task<string> GetAddress();
    Task<string> SignPersonalSign(string message);       // EIP-191
    Task<string> SignTypedData(Eip712TypedData data);   // EIP-712
}

// Custodial (game studio handles fiat ↔ crypto)
public interface IAMPCustodialProvider
{
    Task<string> GetAddress(string playerId);
    Task<string> SignPersonalSign(string playerId, string message);
    Task<string> SignTypedData(string playerId, Eip712TypedData data);
    Task FundMatch(string matchId, string playerId);
    Task WithdrawWinnings(string matchId, string playerId);
}
```

### Built-in Signers

| Signer | Use case | Notes |
|---|---|---|
| `PrivateKeySigner` | Server-side, testing | EIP-191 + EIP-712 via Nethereum |
| `IAMPCustodialProvider` | Fiat-friendly games | You implement the interface |

## Tests

```bash
dotnet test    # 18 tests (16 unit + 2 live integration)
```

## License

Apache-2.0
