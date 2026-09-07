using Xunit;
using Amp.Sdk;
using Amp.Sdk.Signers;
using Amp.Sdk.Crypto;

namespace AmpSdk.Tests;

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
        for (int i = 0; i < 100; i++)
            salts.Add(CryptoHelpers.GenerateSalt());
        Assert.Equal(100, salts.Count);
    }

    [Fact]
    public void BuildReportMessage_CorrectFormat()
    {
        var msg = CryptoHelpers.BuildReportMessage("match-123", "win");
        Assert.Equal("AMP_REPORT:v1:match-123:win", msg);
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
        Assert.Equal(66, result.Length); // 0x + 64 hex chars
        Assert.EndsWith("01", result);
    }
}

public class SignerTests
{
    [Fact]
    public async Task PrivateKeySigner_DerivesAddress()
    {
        var signer = new PrivateKeySigner("0x" + new string('d', 1) + new string('b', 63));
        var addr = await signer.GetAddress();
        Assert.Matches(@"^0x[0-9a-fA-F]{40}$", addr);
    }

    [Fact]
    public async Task PrivateKeySigner_SignsPersonalSign()
    {
        var signer = new PrivateKeySigner("0x" + new string('d', 1) + new string('b', 63));
        var sig = await signer.SignPersonalSign("hello world");
        Assert.Matches(@"^0x[0-9a-f]{130}$", sig);
    }
}

public class ClientTests
{
    [Fact]
    public void Constructor_WithSigner()
    {
        var amp = new AMPClient(
            "https://amp.playwithamp.xyz",
            new PrivateKeySigner("0x" + new string('d', 1) + new string('b', 63))
        );
        Assert.NotNull(amp);
        Assert.False(amp.Authenticated);
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
    }

    [Fact]
    public async Task Login_WithoutSigner_Throws()
    {
        var amp = new AMPClient("https://amp.playwithamp.xyz");
        await Assert.ThrowsAsync<InvalidOperationException>(() => amp.LoginAsync());
    }
}

public class TestCustodialProvider : IAMPCustodialProvider
{
    public Task<string> GetAddress(string playerId) => Task.FromResult("0x0000000000000000000000000000000000000001");
    public Task<string> SignPersonalSign(string playerId, string message) => Task.FromResult("0x" + new string('0', 130));
    public Task<string> SignTypedData(string playerId, Eip712TypedData typedData) => Task.FromResult("0x" + new string('0', 130));
    public Task FundMatch(string matchId, string playerId) => Task.CompletedTask;
    public Task WithdrawWinnings(string matchId, string playerId) => Task.CompletedTask;
}
