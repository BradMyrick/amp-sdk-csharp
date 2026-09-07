// Example: N-Player Multiplayer — commit, reveal, play, exit certs, report, claim
//
// The full staked FFA (free-for-all) lifecycle for N-player games:
// commit-reveal anti-collusion, live play with death certificates,
// EIP-712 ladder reporting, and settlement.
//
// Run: dotnet run --project examples/Multiplayer.csproj

using Amp.Sdk;
using Amp.Sdk.Signers;
using System.Text.Json;

var amp = new AMPClient(
    "https://amp.playwithamp.xyz",
    new PrivateKeySigner(Environment.GetEnvironmentVariable("TEST_KEY")!)
);

// ═══════════════════════════════════════════════════════════════
// 1. LOGIN + COMMIT — stake into the FFA queue
// ═══════════════════════════════════════════════════════════════

await amp.LoginAsync();
Console.WriteLine($"Logged in: {amp.Wallet}");

// multiCommit generates a salt internally and returns it — KEEP IT.
// The server only sees keccak256(wallet ‖ stake ‖ salt) until reveal,
// so neither the server nor other players can front-run your stake.
const long stakeWei = 0; // 0 for free-play testing; real value in Wei
var commit = await amp.MultiCommitAsync("amp-tactics", stakeWei, lobbySize: 4);
Console.WriteLine($"Committed ({commit.CommittedCount}/4 waiting), salt: {commit.Salt[..18]}…");

// ═══════════════════════════════════════════════════════════════
// 2. WAIT FOR LOBBY — WebSocket push when the lobby fills
// ═══════════════════════════════════════════════════════════════

var lobbyReady = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
using var sub = amp.OnJson("multi_lobby_formed", lobby =>
{
    Console.WriteLine($"Lobby formed: match {lobby.GetProperty("matchId")}");
    lobbyReady.TrySetResult(lobby);
});

// In production you wait here. In this example we reveal immediately —
// the server keeps your commit and matches you when the lobby fills:
await amp.MultiRevealAsync("amp-tactics", "ranked-1v1", commit.Salt);
Console.WriteLine("Revealed — waiting for the lobby to fill…");

// var lobby = await lobbyReady.Task.WaitAsync(TimeSpan.FromMinutes(2));
// var matchId = lobby.GetProperty("matchId").GetString()!;

// ═══════════════════════════════════════════════════════════════
// 3. PLAY — when a player is eliminated they submit a death cert
// ═══════════════════════════════════════════════════════════════

// When YOUR player dies, sign and submit the certificate, then you can
// disconnect — the signature unlocks your reporting bond at settlement:
//
//   await amp.SubmitExitCertAsync(
//       matchId: matchId,
//       rank: 3,                     // your final place
//       exitFrame: 1200,             // the frame you died on
//       stateHash: "0x…");           // keccak256 of your final game state
//
// Survivors countersign each certificate against their own simulation:
//
//   await amp.CountersignExitCertAsync(matchId, eliminatedWallet, "0x…");

// ═══════════════════════════════════════════════════════════════
// 4. REPORT — the last survivor submits the full ladder (EIP-712)
// ═══════════════════════════════════════════════════════════════

// [winner, second, third, …] — best to worst:
//
//   await amp.MultiReportAsync(
//       matchId,
//       ranked: new[] { (winnerWallet, 1), (secondWallet, 2), (thirdWallet, 3) },
//       transcriptHash: "0x…",       // keccak256 of the game transcript
//       sessionNonce: 42);
//
// The SDK signs EIP-712 typed data (AMPMultiplayer domain); when a
// quorum of survivors submits concordant ladders, the match is settled.

// ═══════════════════════════════════════════════════════════════
// 5. CLAIM — trigger settlement and payouts
// ═══════════════════════════════════════════════════════════════

//   await amp.MultiClaimAsync(matchId);

amp.Disconnect();
Console.WriteLine("Done.");
