# Amp.Sdk

[![NuGet version](https://img.shields.io/nuget/v/Amp.Sdk.svg)](https://www.nuget.org/packages/Amp.Sdk)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![Avalanche Fuji](https://img.shields.io/badge/Avalanche%20Fuji-43113-red.svg)](https://testnet.snowtrace.io/address/0x7C743c1C9AE3e7A65d030098F2249B7787d66dfF)
[![Avalanche Build Games](https://img.shields.io/badge/Avalanche-Build%20Games%20Winner-E84142.svg)](https://playwithamp.xyz)

> **Winner of the $15,000 Avalanche Build Games grant.**
>
> **The official Avalanche Tournament & Match Protocol SDK for .NET, Unity, and Godot.**  
> Sponsor-funded tournament prize pools, EIP-712 attested brackets, pull-payment prize claims on Avalanche (`AMPTournamentCup`), and ranked matchmaking with Glicko-2 skill ratings.

---

## Installation

```bash
dotnet add package Amp.Sdk
```

---

## 🏆 1. Avalanche Tournament Protocol (`AmpTournamentClient`)

Interact directly with the **`AMPTournamentCup`** smart contract deployed on **Avalanche Fuji Testnet** (`0x7C743c1C9AE3e7A65d030098F2249B7787d66dfF`).

### Contract Details

| Parameter | Value |
|---|---|
| **Network** | Avalanche Fuji Testnet (Chain ID: `43113`) |
| **RPC Endpoint** | `https://api.avax-test.network/ext/bc/C/rpc` |
| **Contract** | `0x7C743c1C9AE3e7A65d030098F2249B7787d66dfF` ([Snowtrace](https://testnet.snowtrace.io/address/0x7C743c1C9AE3e7A65d030098F2249B7787d66dfF)) |
| **Selectors** | `getTournament(uint256)`: `0x1a5bd7fc`<br>`claimed(uint256,uint256)`: `0xa54ab457`<br>`claimPrize(uint256,uint256)`: `0x7b154140` |
| **EIP-712 Attestation** | `TournamentResult(uint256 tournamentId,address[] winners)` |

### Read Tournament Details & Check Claims

```csharp
using System.Numerics;
using Amp.Sdk;

// 1. Connect to Avalanche Fuji
var client = new AmpTournamentClient();

// 2. Fetch tournament info (e.g. Tournament #8)
var tournamentId = new BigInteger(8);
var tournament = await client.GetTournamentAsync(tournamentId);

Console.WriteLine($"🏆 Tournament #{tournament.Id} [{tournament.State}]");
Console.WriteLine($"Prize Pool: {tournament.PrizePoolAvax:F2} AVAX");
Console.WriteLine($"Payout Split: {string.Join(" / ", tournament.PayoutBps)} bps");

// 3. Verify if player is a winner and eligible to claim
var playerAddress = "0x996204c3e14fc538aFA059b30368d0Fa8f12AD9a";
var status = await client.CanClaimPrizeAsync(tournamentId, playerAddress);

if (status.CanClaim)
{
    Console.WriteLine($"Winner! Placement #{status.Placement + 1}, Claimable: {status.EstimatedPrizeAvax:F4} AVAX");
}
```

### Claim Prize On-Chain

```csharp
using Amp.Sdk;

// Initialize client with winner's private key
var client = AmpTournamentClient.FromPrivateKey(winnerPrivateKey);

// Broadcast claimPrize transaction
var txHash = await client.ClaimPrizeAsync(tournamentId, placement);
Console.WriteLine($"Claim broadcast! Tx: https://testnet.snowtrace.io/tx/{txHash}");
```

### Verifier Attestation (EIP-712)

Attest tournament standings with a cryptographic EIP-712 signature over `TournamentResult(uint256 tournamentId,address[] winners)`:

```csharp
using Amp.Sdk;

var verifierClient = AmpTournamentClient.FromPrivateKey(verifierPrivateKey);

var winners = new[]
{
    "0x1563915e194D8CfBA1943570603F7606A3115508",
    "0x5CbDd86a2FA8Dc4bDdd8a8f69dBa48572EeC07FB"
};

// Sign EIP-712 attestation
var signature = await verifierClient.SignTournamentResultAsync(tournamentId, winners);
```

### Tournament Models

- **`TournamentState`**: `Open` (0), `Finalized` (1), `Complete` (2), `Cancelled` (3).
- **`TournamentData`**: `Id`, `Sponsor`, `Token`, `PrizePoolWei`, `PrizePoolAvax`, `PayoutBps`, `Verifier`, `Winners`, `State`, `CreatedAt`, `FinalizeDeadline`.
- **`CanClaimResult`**: `CanClaim`, `IsWinner`, `Placement` (0-indexed), `EstimatedPrizeWei`, `EstimatedPrizeAvax`, `IsClaimed`, `Reason`.

---

## ⚡ 2. Ranked Matchmaking & Ratings (`AMPClient`)

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

---

## Examples

| Example | What it covers |
|---|---|
| [`Tournament.cs`](examples/Tournament.cs) | **Avalanche Tournament Protocol**: fetch cups, verify claims, EIP-712 attestations, claim prizes |
| [`QuickStart.cs`](examples/QuickStart.cs) | Full matchmaking lifecycle: login → queue → match → report → ratings |
| [`Custodial.cs`](examples/Custodial.cs) | Fiat path: PayPal ↔ AVAX, custodial wallet model for web2 gamers |
| [`Multiplayer.cs`](examples/Multiplayer.cs) | N-player FFA lobby, commit-reveal escrow, death certificates |

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Your Game (Unity, Godot, .NET Game Server)            │
│  ┌─────────────────────────────┐ ┌────────────────────┐ │
│  │ AmpTournamentClient         │ │ AMPClient          │ │
│  │ • Direct Avalanche Fuji     │ │ • Matchmaking      │ │
│  │ • AMPTournamentCup.sol      │ │ • Glicko-2 ratings │ │
│  │ • EIP-712 Attestation       │ │ • Queue & parties  │ │
│  │ • Pull-payment claims       │ │ • WebSocket push   │ │
│  └──────────────┬──────────────┘ └──────────┬─────────┘ │
└─────────────────┼───────────────────────────┼───────────┘
                  │ eth_call / tx             │ REST + WS
                  ▼                           ▼
        ┌───────────────────┐       ┌───────────────────┐
        │ Avalanche C-Chain │       │ AMP Matchmaker    │
        │ Fuji Testnet      │       │ (Rust Engine)     │
        │ 0x7C743...6dfF    │       │ playwithamp.xyz   │
        └───────────────────┘       └───────────────────┘
```

---

## API Reference

### `AmpTournamentClient`

| Method | Description |
|---|---|
| `GetTournamentAsync(tournamentId)` | Reads on-chain tournament data (`getTournament`) via `eth_call` |
| `IsPrizeClaimedAsync(tournamentId, placement)` | Checks if a placement prize is claimed (`claimed` mapping) |
| `CanClaimPrizeAsync(tournamentId, walletAddress)` | Evaluates winner status, placement, eligibility, and prize amount |
| `ClaimPrizeAsync(tournamentId, placement)` | Executes the on-chain `claimPrize` transaction |
| `SignTournamentResultAsync(tournamentId, winners, [signer])` | Generates EIP-712 attestation signature over `TournamentResult` |
| `ComputeTournamentResultDigest(tournamentId, winners)` | Computes the 32-byte EIP-712 digest matching the contract |
| `GetNextTournamentIdAsync()` | Reads `nextTournamentId` counter from the contract |
| `GetDomainSeparatorAsync()` | Reads contract `domainSeparator()` |

### `AMPClient`

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
| `SubmitExitCertAsync(matchId, rank, exitFrame, stateHash)` | Submit a death cert on elimination |
| `CountersignExitCertAsync(matchId, wallet, stateHash)` | Survivor verifies an exit cert |
| `VerifyEscrowAsync(matchId)` | Verify on-chain escrow for staked 1v1 |
| `MultiClaimAsync(matchId)` | Trigger settlement |
| `WaitForMatchAsync(timeout?)` | One call: queue → wait → `MatchFound` |
| `On<T>(eventType, handler)` | Subscribe to WebSocket event |
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

### Signer Interfaces & Dual-Path Model

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

| Signer | Use case | Notes |
|---|---|---|
| `PrivateKeySigner` | Server-side, tournaments, testing | EIP-191 + EIP-712 via Nethereum |
| `IAMPCustodialProvider` | Fiat-friendly games | You implement the interface |

---

## Tests

```bash
dotnet test    # 43 tests (37 unit & golden vector tests + 6 live Fuji testnet integration tests)
```

---

## License

Apache-2.0
