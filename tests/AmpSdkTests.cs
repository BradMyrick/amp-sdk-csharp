using Xunit;
using Amp.Sdk;
using Amp.Sdk.Signers;
using Amp.Sdk.Crypto;

namespace AmpSdk.Tests;

// ═══════════════════════════════════════════════════════════════
// CRYPTO TESTS
// ═══════════════════════════════════════════════════════════════

public class CryptoTests
{
    [Fact]
    public void GenerateSalt_ProducesValidHex()
    {
        var salt = CryptoHelpers.GenerateSalt();
        Assert.Matches(@"^0x[0-9a-f]{64}$", salt);
    }

    [Fact]
    public void GenerateSalt_ProducesUniqueValues()
    {
        var salts = new HashSet<string>();
        for (var i = 0; i < 100; i++)
            salts.Add(CryptoHelpers.GenerateSalt());
        Assert.Equal(100, salts.Count);
    }

    [Fact]
    public void BuildReportMessage_CorrectFormat()
    {
        Assert.Equal("AMP_REPORT:v1:match-123:win", CryptoHelpers.BuildReportMessage("match-123", "win"));
        Assert.Equal("AMP_REPORT:v1:m:loss", CryptoHelpers.BuildReportMessage("m", "loss"));
        Assert.Equal("AMP_REPORT:v1:m:draw", CryptoHelpers.BuildReportMessage("m", "draw"));
    }

    [Fact]
    public void ToHex_EncodesCorrectly()
    {
        Assert.Equal("0x68656c6c6f", CryptoHelpers.ToHex("hello"));
        Assert.Equal("0x", CryptoHelpers.ToHex(""));
    }

    [Fact]
    public void ToBytes32_PadsCorrectly()
    {
        var result = CryptoHelpers.ToBytes32(1);
        Assert.StartsWith("0x", result);
        Assert.Equal(66, result.Length);
        Assert.EndsWith("01", result);
    }

    [Fact]
    public async Task ComputeCommitHash_IsDeterministic()
    {
        var wallet = "0x95CC495dF579981d3Ffa4a8f77B93A17563E077a";
        var hash1 = await CryptoHelpers.ComputeCommitHashAsync(wallet, 1000, "test-salt");
        var hash2 = await CryptoHelpers.ComputeCommitHashAsync(wallet, 1000, "test-salt");
        Assert.Equal(hash1, hash2);
        Assert.Matches(@"^0x[0-9a-f]{64}$", hash1);
    }

    [Fact]
    public async Task ComputeCommitHash_MatchesCrossSdkGoldenVector()
    {
        // Identical in the TS/C#/C++/Rust SDKs and amp-server:
        // keccak256(addr20 | stake_u64_be(8) | salt-utf8)
        var h = await CryptoHelpers.ComputeCommitHashAsync(
            "0x95CC495dF579981d3Ffa4a8f77B93A17563E077a",
            1_000_000_000_000_000, "0xdeadbeef");
        Assert.Equal(
            "0x2d5491f1ad0117eea0c302b3cfb07590fef2d3892349e017361afd1bb5e5be10",
            h.ToLowerInvariant());
    }

    [Fact]
    public async Task ComputeCommitHash_IsInputSensitive()
    {
        var wallet = "0x95CC495dF579981d3Ffa4a8f77B93A17563E077a";
        var base_ = await CryptoHelpers.ComputeCommitHashAsync(wallet, 100, "salt");
        var wrongStake = await CryptoHelpers.ComputeCommitHashAsync(wallet, 200, "salt");
        var wrongSalt = await CryptoHelpers.ComputeCommitHashAsync(wallet, 100, "other");
        Assert.NotEqual(base_, wrongStake);
        Assert.NotEqual(base_, wrongSalt);
    }

    [Fact]
    public void BuildLadderTypedData_CorrectStructure()
    {
        var td = CryptoHelpers.BuildLadderTypedData(
            chainId: 43113,
            contractAddress: "0xcabf7b626172fE55d54f03c346563671AbcC77f7",
            matchId: "0x" + new string('a', 64),
            gameId: "0x" + new string('0', 63) + "1",
            rankedPlacements: new[] { "0x95CC495dF579981d3Ffa4a8f77B93A17563E077a", "0x79aDcEF0E2bdc030f5906aA80C6B50C3712c0064" },
            transcriptHash: "0x" + new string('b', 64),
            sessionNonce: 42);

        Assert.Equal("AMPMultiplayer", td.Name);
        Assert.Equal("1", td.Version);
        Assert.Equal(43113, td.ChainId);
        Assert.Equal("MultiplayerLadder", td.PrimaryType);
        Assert.Equal(5, td.Types["MultiplayerLadder"].Length);
    }
}

// ═══════════════════════════════════════════════════════════════
// SIGNER TESTS
// ═══════════════════════════════════════════════════════════════

public class SignerTests
{
    private const string TestKey = "0x" + "d" + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task PrivateKeySigner_DerivesAddress()
    {
        var signer = new PrivateKeySigner(TestKey);
        var addr = await signer.GetAddress();
        Assert.Matches(@"^0x[0-9a-fA-F]{40}$", addr);
    }

    [Fact]
    public async Task PrivateKeySigner_AddressIsDeterministic()
    {
        var s1 = new PrivateKeySigner(TestKey);
        var s2 = new PrivateKeySigner(TestKey);
        Assert.Equal(await s1.GetAddress(), await s2.GetAddress());
    }

    [Fact]
    public async Task PrivateKeySigner_SignsPersonalSign()
    {
        var signer = new PrivateKeySigner(TestKey);
        var sig = await signer.SignPersonalSign("hello world");
        // 65 bytes = 130 hex chars + 0x prefix
        Assert.Matches(@"^0x[0-9a-f]{130}$", sig.ToLowerInvariant());
    }

    [Fact]
    public async Task PrivateKeySigner_SignTypedData_Produces65ByteSig()
    {
        var signer = new PrivateKeySigner(TestKey);
        var td = CryptoHelpers.BuildLadderTypedData(
            43113,
            "0xcabf7b626172fE55d54f03c346563671AbcC77f7",
            "0x" + new string('a', 64),
            "0x" + new string('0', 63) + "1",
            new[] { "0x95CC495dF579981d3Ffa4a8f77B93A17563E077a" },
            "0x" + new string('b', 64),
            42);

        var sig = await signer.SignTypedData(td);
        Assert.Matches(@"^0x[0-9a-f]{130}$", sig.ToLowerInvariant());

        // v byte should be 27 or 28
        var v = Convert.ToInt32(sig.Substring(130, 2), 16);
        Assert.True(v == 27 || v == 28, $"v was {v}");
    }
}

// ═══════════════════════════════════════════════════════════════
// CLIENT TESTS
// ═══════════════════════════════════════════════════════════════

public class ClientTests
{
    [Fact]
    public void Constructor_WithSigner()
    {
        var amp = new AMPClient(
            "https://amp.playwithamp.xyz",
            new PrivateKeySigner("0x" + "d" + new string('b', 63))
        );
        Assert.NotNull(amp);
        Assert.False(amp.Authenticated);
        Assert.Null(amp.Wallet);
    }

    [Fact]
    public void Constructor_WithCustodial()
    {
        var amp = new AMPClient(
            "https://amp.playwithamp.xyz",
            custodial: new TestCustodialProvider(),
            playerId: "test-player"
        );
        Assert.NotNull(amp);
        Assert.False(amp.Authenticated);
    }

    [Fact]
    public async Task Login_WithoutSigner_Throws()
    {
        var amp = new AMPClient("https://amp.playwithamp.xyz");
        await Assert.ThrowsAsync<InvalidOperationException>(() => amp.LoginAsync());
    }

    [Fact]
    public void ApiMethods_Exist()
    {
        var amp = new AMPClient("https://example.com",
            new PrivateKeySigner("0x" + "d" + new string('b', 63)));

        // Verify all methods exist
        Assert.NotNull(amp.GetType().GetMethod("LoginAsync"));
        Assert.NotNull(amp.GetType().GetMethod("Logout"));
        Assert.NotNull(amp.GetType().GetMethod("MeAsync"));
        Assert.NotNull(amp.GetType().GetMethod("GetPlayerAsync"));
        Assert.NotNull(amp.GetType().GetMethod("GamesAsync"));
        Assert.NotNull(amp.GetType().GetMethod("JoinQueueAsync"));
        Assert.NotNull(amp.GetType().GetMethod("LeaveQueueAsync"));
        Assert.NotNull(amp.GetType().GetMethod("QueueStatusAsync"));
        Assert.NotNull(amp.GetType().GetMethod("PlayBotAsync"));
        Assert.NotNull(amp.GetType().GetMethod("GetMatchAsync"));
        Assert.NotNull(amp.GetType().GetMethod("MatchHistoryAsync"));
        Assert.NotNull(amp.GetType().GetMethod("ReportMatchAsync"));
        Assert.NotNull(amp.GetType().GetMethod("CreatePartyAsync"));
        Assert.NotNull(amp.GetType().GetMethod("JoinPartyAsync"));
        Assert.NotNull(amp.GetType().GetMethod("GetPartyAsync"));
        Assert.NotNull(amp.GetType().GetMethod("LockPartyAsync"));
        Assert.NotNull(amp.GetType().GetMethod("DisbandPartyAsync"));
        Assert.NotNull(amp.GetType().GetMethod("MultiCommitAsync"));
        Assert.NotNull(amp.GetType().GetMethod("MultiRevealAsync"));
        Assert.NotNull(amp.GetType().GetMethod("GetMultiMatchAsync"));
        Assert.NotNull(amp.GetType().GetMethod("MultiReportAsync"));
        Assert.NotNull(amp.GetType().GetMethod("MultiClaimAsync"));
        Assert.NotNull(amp.GetType().GetMethod("On"));
        Assert.NotNull(amp.GetType().GetMethod("Disconnect"));
    }
}

// ═══════════════════════════════════════════════════════════════
// LIVE INTEGRATION TESTS (against production matchmaker)
// ═══════════════════════════════════════════════════════════════

[Collection("Live")]
public class LiveIntegrationTests
{
    private const string TestKey = "0x" + "d" + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Server = "https://amp.playwithamp.xyz";

    [Fact]
    public async Task Full_Login_Games_Me_Party_Match_Report_Lifecycle()
    {
        var amp = new AMPClient(Server, new PrivateKeySigner(TestKey));
        try
        {
            // 1. Login
            var player = await amp.LoginAsync();
            Assert.Matches(@"^0x[0-9a-fA-F]{40}$", player.Wallet);
            Assert.True(amp.Authenticated);

            // 2. Games
            var games = await amp.GamesAsync();
            Assert.NotEmpty(games.Games);

            // 3. Player profile
            var me = await amp.MeAsync();
            Assert.Equal(player.Wallet.ToLowerInvariant(), me.Wallet.ToLowerInvariant());

            // 4. Create + disband party
            var party = await amp.CreatePartyAsync("amp-tactics", "ranked-1v1");
            Assert.NotEmpty(party.PartyId);
            Assert.Equal(6, party.InviteCode.Length);
            await amp.DisbandPartyAsync(party.PartyId);

            // 5. Play a bot match
            var bot = await amp.PlayBotAsync();
            Assert.NotEmpty(bot.MatchId);
            Assert.True(bot.Bot);

            // 6. Get match details
            var match = await amp.GetMatchAsync(bot.MatchId);
            Assert.NotEmpty(match.MatchId);

            // 7. Report the result
            var report = await amp.ReportMatchAsync(bot.MatchId, "win");
            Assert.Equal(bot.MatchId, report.MatchId);

            // 8. Match history
            var history = await amp.MatchHistoryAsync(5);
            Assert.NotNull(history);
        }
        finally
        {
            amp.Dispose();
        }
    }

    [Fact]
    public async Task Queue_Join_Status_Leave()
    {
        var amp = new AMPClient(Server, new PrivateKeySigner(TestKey));
        try
        {
            await amp.LoginAsync();

            // Clean up any stale live match
            try
            {
                var me = await amp.MeAsync();
                if (me.LiveMatchId != null)
                    await amp.ReportMatchAsync(me.LiveMatchId, "win");
            }
            catch { /* no stale match */ }

            // Join queue
            var result = await amp.JoinQueueAsync("amp-tactics", "ranked-1v1");
            Assert.NotEmpty(result.TicketId);

            // Check status
            var status = await amp.QueueStatusAsync();
            Assert.True(status.Queued);

            // Leave
            await amp.LeaveQueueAsync();
            var after = await amp.QueueStatusAsync();
            Assert.False(after.Queued);
        }
        finally
        {
            amp.Dispose();
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// TEST HELPERS
// ═══════════════════════════════════════════════════════════════

public class TestCustodialProvider : IAMPCustodialProvider
{
    public Task<string> GetAddress(string playerId) =>
        Task.FromResult("0x0000000000000000000000000000000000000001");
    public Task<string> SignPersonalSign(string playerId, string message) =>
        Task.FromResult("0x" + new string('0', 130));
    public Task<string> SignTypedData(string playerId, Eip712TypedData typedData) =>
        Task.FromResult("0x" + new string('0', 130));
    public Task FundMatch(string matchId, string playerId) => Task.CompletedTask;
    public Task WithdrawWinnings(string matchId, string playerId) => Task.CompletedTask;
}

[CollectionDefinition("Live")]
public class LiveCollection : ICollectionFixture<LiveCollection>
{
    // Ensures live tests don't run in parallel (they share a test wallet)
}
