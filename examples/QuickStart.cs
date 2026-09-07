// Example: Quick Start — Complete Integration in One File
//
// Shows the full lifecycle: login → queue → match → report → result.
// Copy the parts you need into your Unity/Godot/.NET game.
//
// Run: dotnet run --project examples/QuickStart.csproj

using Amp.Sdk;
using Amp.Sdk.Signers;

// ═══════════════════════════════════════════════════════════════
// SETUP — create the client with a signer
// ═══════════════════════════════════════════════════════════════

// For server-side games / bots (uses a private key):
var amp = new AMPClient(
    "https://amp.playwithamp.xyz",
    new PrivateKeySigner(Environment.GetEnvironmentVariable("TEST_KEY")!)
);

// For Unity with a wallet plugin, implement IAMPSigner:
// var amp = new AMPClient("https://amp.playwithamp.xyz", myWalletSigner);

// ═══════════════════════════════════════════════════════════════
// 1. LOGIN — one gasless signature
// ═══════════════════════════════════════════════════════════════

var player = await amp.LoginAsync();
Console.WriteLine($"Logged in: {player.Wallet}");

// ═══════════════════════════════════════════════════════════════
// 2. CHECK GAMES — see what's available
// ═══════════════════════════════════════════════════════════════

var games = await amp.GamesAsync();
foreach (var game in games.Games)
{
    Console.WriteLine($"{game.Name} ({game.Id})");
    foreach (var rs in game.Rulesets)
    {
        Console.WriteLine($"  {rs.Name} — {rs.QueueDepth} in queue");
    }
}

// ═══════════════════════════════════════════════════════════════
// 3. JOIN QUEUE — enter the ranked matchmaking queue
// ═══════════════════════════════════════════════════════════════

var queueResult = await amp.JoinQueueAsync("amp-tactics", "ranked-1v1");
Console.WriteLine($"Queued (depth: {queueResult.QueueDepth})");

// ═══════════════════════════════════════════════════════════════
// 4. LISTEN FOR MATCH — WebSocket push
// ═══════════════════════════════════════════════════════════════

amp.On<MatchFound>("match_found", async match =>
{
    Console.WriteLine($"\n⚔️  Match: vs {match.Opponent.Wallet} ({match.Opponent.Rating} MMR)");
    Console.WriteLine($"   Your rating: {match.YourRating}");

    // Run your game here — replace with your actual game logic
    var result = new Random().Next(2) == 0 ? "win" : "loss";

    // Report the result (auto-signs EIP-191)
    await amp.ReportMatchAsync(match.MatchId, result);
    Console.WriteLine($"   Reported: {result}");
});

// ═══════════════════════════════════════════════════════════════
// 5. LISTEN FOR RESULT — rating update
// ═══════════════════════════════════════════════════════════════

amp.OnJson("match_result", result =>
{
    var you = result.GetProperty("you");
    Console.WriteLine($"\n{(result.GetProperty("won").GetBoolean() ? "🏆" : "💀")} " +
        $"{Math.Round(you.GetProperty("ratingBefore").GetDouble())} → " +
        $"{Math.Round(you.GetProperty("ratingAfter").GetDouble())}");
});

// ═══════════════════════════════════════════════════════════════
// ALTERNATIVES
// ═══════════════════════════════════════════════════════════════

// Play a bot immediately (skip the queue wait):
// var bot = await amp.PlayBotAsync();
// await amp.ReportMatchAsync(bot.MatchId, "win");

// Leave the queue:
// await amp.LeaveQueueAsync();

// Create a party:
// var party = await amp.CreatePartyAsync("amp-tactics", "ranked-1v1");
// Console.WriteLine($"Invite code: {party.InviteCode}");
// // Friends join with the code, then the leader locks:
// await amp.LockPartyAsync(party.PartyId);

// ═══════════════════════════════════════════════════════════════
// WHAT AMP HANDLES VS WHAT YOU HANDLE
// ═══════════════════════════════════════════════════════════════
//
// AMP handles:                    Your game handles:
//   Skill ratings (Glicko-2)        Determining who won
//   Matchmaking queue               Running the game
//   Match assignment                Game UI/UX
//   Result verification             Player experience
//   On-chain escrow + payouts       Your game's economy
//   Anti-collusion                  Your game's rules

Console.WriteLine("\nWaiting for match... (Ctrl+C to stop)");
await Task.Delay(Timeout.Infinite);
