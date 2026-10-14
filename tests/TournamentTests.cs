using System.Numerics;
using Amp.Sdk;
using Amp.Sdk.Crypto;
using Amp.Sdk.Signers;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace AmpSdk.Tests;

// ═══════════════════════════════════════════════════════════════
// ABI & CALLDATA ENCODING / DECODING TESTS
// ═══════════════════════════════════════════════════════════════

public class TournamentAbiTests
{
    // Real raw hex response from Fuji contract for Tournament #1
    private const string Tournament1RawHex =
        "0x0000000000000000000000000000000000000000000000000000000000000020" +
        "00000000000000000000000095cc495df579981d3ffa4a8f77b93a17563e077a" +
        "0000000000000000000000000000000000000000000000000000000000000000" +
        "00000000000000000000000000000000000000000000000000b1a2bc2ec50000" +
        "0000000000000000000000000000000000000000000000000000000000000120" +
        "00000000000000000000000019e7e376e7c213b7e7e7e46cc70a5dd086daff2a" +
        "0000000000000000000000000000000000000000000000000000000000000180" +
        "0000000000000000000000000000000000000000000000000000000000000002" +
        "000000000000000000000000000000000000000000000000000000006a74be98" +
        "000000000000000000000000000000000000000000000000000000006a761014" +
        "0000000000000000000000000000000000000000000000000000000000000002" +
        "0000000000000000000000000000000000000000000000000000000000001b58" +
        "0000000000000000000000000000000000000000000000000000000000000bb8" +
        "0000000000000000000000000000000000000000000000000000000000000002" +
        "0000000000000000000000001563915e194d8cfba1943570603f7606a3115508" +
        "0000000000000000000000005cbdd86a2fa8dc4bddd8a8f69dba48572eec07fb";

    // Real raw hex response from Fuji contract for Tournament #8
    private const string Tournament8RawHex =
        "0x0000000000000000000000000000000000000000000000000000000000000020" +
        "0000000000000000000000009d4e138cbfc038e6e2b92be4e97925189f687d71" +
        "0000000000000000000000000000000000000000000000000000000000000000" +
        "0000000000000000000000000000000000000000000000000de0b6b3a7640000" +
        "0000000000000000000000000000000000000000000000000000000000000120" +
        "0000000000000000000000009d4e138cbfc038e6e2b92be4e97925189f687d71" +
        "00000000000000000000000000000000000000000000000000000000000001a0" +
        "0000000000000000000000000000000000000000000000000000000000000001" +
        "000000000000000000000000000000000000000000000000000000006a754808" +
        "000000000000000000000000000000000000000000000000000000006a7e8283" +
        "0000000000000000000000000000000000000000000000000000000000000003" +
        "0000000000000000000000000000000000000000000000000000000000001770" +
        "0000000000000000000000000000000000000000000000000000000000000bb8" +
        "00000000000000000000000000000000000000000000000000000000000003e8" +
        "0000000000000000000000000000000000000000000000000000000000000003" +
        "000000000000000000000000996204c3e14fc538afa059b30368d0fa8f12ad9a" +
        "000000000000000000000000c501d81254cc75149819bdac5c82901d77707b51" +
        "0000000000000000000000005370f5c942a2cae91eaa76f06e1f12d3233abc0d";

    [Fact]
    public void EncodeGetTournament_MatchesStandardSelector()
    {
        var calldata8 = AmpTournamentClient.EncodeGetTournament(8);
        Assert.Equal(
            "0x1a5bd7fc0000000000000000000000000000000000000000000000000000000000000008",
            calldata8);

        var calldata1 = AmpTournamentClient.EncodeGetTournament(1);
        Assert.Equal(
            "0x1a5bd7fc0000000000000000000000000000000000000000000000000000000000000001",
            calldata1);
    }

    [Fact]
    public void EncodeClaimed_MatchesStandardSelector()
    {
        var calldata = AmpTournamentClient.EncodeClaimed(8, 1);
        Assert.Equal(
            "0xa54ab45700000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000001",
            calldata);
    }

    [Fact]
    public void EncodeClaimPrize_MatchesStandardSelector()
    {
        var calldata = AmpTournamentClient.EncodeClaimPrize(8, 0);
        Assert.Equal(
            "0x7b15414000000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000",
            calldata);
    }

    [Fact]
    public void DecodeBool_ParsesCorrectly()
    {
        var trueHex = "0x0000000000000000000000000000000000000000000000000000000000000001";
        var falseHex = "0x0000000000000000000000000000000000000000000000000000000000000000";

        Assert.True(AmpTournamentClient.DecodeBool(trueHex));
        Assert.False(AmpTournamentClient.DecodeBool(falseHex));
    }

    [Fact]
    public void DecodeTournament_DecodesTournament1Accurately()
    {
        var t = AmpTournamentClient.DecodeTournament(1, Tournament1RawHex);

        Assert.Equal(new BigInteger(1), t.Id);
        Assert.Equal("0x95CC495dF579981d3Ffa4a8f77B93A17563E077a", t.Sponsor);
        Assert.True(t.IsNativeAvax);
        Assert.Equal(new BigInteger(50000000000000000), t.PrizePoolWei);
        Assert.Equal(0.05m, t.PrizePoolAvax);
        Assert.Equal(TournamentState.Complete, t.State);
        Assert.True(t.IsComplete);
        Assert.False(t.IsOpen);
        Assert.Equal(2, t.PayoutBps.Length);
        Assert.Equal(7000, t.PayoutBps[0]);
        Assert.Equal(3000, t.PayoutBps[1]);
        Assert.Equal(2, t.Winners.Length);
        Assert.Equal("0x1563915e194D8CfBA1943570603F7606A3115508", t.Winners[0]);
        Assert.Equal("0x5CbDd86a2FA8Dc4bDdd8a8f69dBa48572EeC07FB", t.Winners[1]);

        // Placement payout calculation
        Assert.Equal(new BigInteger(35000000000000000), t.GetPlacementPrizeWei(0));
        Assert.Equal(0.035m, t.GetPlacementPrizeAvax(0));
        Assert.Equal(new BigInteger(15000000000000000), t.GetPlacementPrizeWei(1));
        Assert.Equal(0.015m, t.GetPlacementPrizeAvax(1));

        // Timestamps
        Assert.True(t.CreatedAt > 0);
        Assert.True(t.FinalizeDeadline > 0);
        Assert.True(t.FinalizeDeadline >= t.CreatedAt);
    }

    [Fact]
    public void DecodeTournament_DecodesTournament8Accurately()
    {
        var t = AmpTournamentClient.DecodeTournament(8, Tournament8RawHex);

        Assert.Equal(new BigInteger(8), t.Id);
        Assert.Equal("0x9d4e138cbfc038e6e2b92be4e97925189f687d71", t.Sponsor.ToLowerInvariant());
        Assert.True(t.IsNativeAvax);
        Assert.Equal(new BigInteger(1_000_000_000_000_000_000), t.PrizePoolWei); // 1 AVAX
        Assert.Equal(1.0m, t.PrizePoolAvax);
        Assert.Equal(TournamentState.Finalized, t.State);
        Assert.True(t.IsFinalized);
        Assert.Equal(3, t.PayoutBps.Length);
        Assert.Equal(6000, t.PayoutBps[0]);
        Assert.Equal(3000, t.PayoutBps[1]);
        Assert.Equal(1000, t.PayoutBps[2]);
        Assert.Equal(3, t.Winners.Length);
        Assert.Equal("0x996204c3e14fc538aFA059b30368d0Fa8f12AD9a", t.Winners[0]);
        Assert.Equal("0xc501D81254CC75149819bdaC5C82901D77707b51", t.Winners[1]);
        Assert.Equal("0x5370F5C942a2CaE91eaA76F06E1F12D3233AbC0D", t.Winners[2]);

        Assert.Equal(0.6m, t.GetPlacementPrizeAvax(0));
        Assert.Equal(0.3m, t.GetPlacementPrizeAvax(1));
        Assert.Equal(0.1m, t.GetPlacementPrizeAvax(2));
    }
}

// ═══════════════════════════════════════════════════════════════
// EIP-712 ATTESTATION & SIGNATURE VERIFICATION TESTS
// ═══════════════════════════════════════════════════════════════

public class TournamentEip712Tests
{
    private const string VerifierKey =
        "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

    private static readonly string[] TestWinners = new[]
    {
        "0x1563915e194D8CfBA1943570603F7606A3115508",
        "0x5CbDd86a2FA8Dc4bDdd8a8f69dBa48572EeC07FB"
    };

    [Fact]
    public void DomainSeparator_MatchesOnChainContract()
    {
        var td = AmpTournamentClient.BuildTournamentResultTypedData(
            43113,
            AmpTournamentClient.DefaultContractAddress,
            42,
            TestWinners
        );

        // Compute domain separator via PrivateKeySigner helper
        var digest = PrivateKeySigner.ComputeEip712Digest(td);
        Assert.NotNull(digest);
        Assert.Equal(32, digest.Length);

        // Verify domain separator directly
        var typeHash = new Nethereum.Util.Sha3Keccack().CalculateHash(
            System.Text.Encoding.UTF8.GetBytes("EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)"));
        var nameHash = new Nethereum.Util.Sha3Keccack().CalculateHash(System.Text.Encoding.UTF8.GetBytes("AMPTournamentCup"));
        var versionHash = new Nethereum.Util.Sha3Keccack().CalculateHash(System.Text.Encoding.UTF8.GetBytes("1"));
        var chainIdWord = AmpTournamentClient.HexToBytes(AmpTournamentClient.BigIntegerToHex32(43113));
        var contractBytes = AmpTournamentClient.HexToBytes(AmpTournamentClient.DefaultContractAddress);
        var contractWord = new byte[32];
        Array.Copy(contractBytes, 0, contractWord, 12, 20);

        var domainInput = new byte[160];
        Array.Copy(typeHash, 0, domainInput, 0, 32);
        Array.Copy(nameHash, 0, domainInput, 32, 32);
        Array.Copy(versionHash, 0, domainInput, 64, 32);
        Array.Copy(chainIdWord, 0, domainInput, 96, 32);
        Array.Copy(contractWord, 0, domainInput, 128, 32);

        var ds = new Nethereum.Util.Sha3Keccack().CalculateHash(domainInput);
        var dsHex = "0x" + Convert.ToHexString(ds).ToLowerInvariant();

        // Exact match with on-chain domainSeparator() from contract on Fuji
        Assert.Equal("0xa06f97ad3d7e36a974ca10b74652528a5c2708ac10140a16de0f3080edf8ccd6", dsHex);
    }

    [Fact]
    public void TournamentResultDigest_MatchesCrossSdkGoldenVector()
    {
        // Vector verified against ethers.TypedDataEncoder on Fuji
        var digest = CryptoHelpers.ComputeTournamentResultDigest(
            43113,
            AmpTournamentClient.DefaultContractAddress,
            42,
            TestWinners
        );

        var digestHex = "0x" + Convert.ToHexString(digest).ToLowerInvariant();
        Assert.Equal("0xb54613dbbd2cf573d412e5d39c98997982087b27df336c4af07662a5b94b5a8d", digestHex);
    }

    [Fact]
    public async Task SignTournamentResult_ProducesExactEthersSignature()
    {
        var client = AmpTournamentClient.FromPrivateKey(VerifierKey);
        var sig = await client.SignTournamentResultAsync(42, TestWinners);

        // Expected signature from ethers.Wallet(verifierKey).signTypedData(...)
        Assert.Equal(
            "0x997d2719534309ae958f42226044f4ab79ac4bf3635fd9ce672035f129a24bb0471b63cab948421595c849ea09d7f8df84bed0cfd8dddaf93f04a4ed5d88473c1c",
            sig.ToLowerInvariant());

        // Recover signer address from signature using EthECKey
        var digest = client.ComputeTournamentResultDigest(42, TestWinners);
        var ethSig = EthECDSASignatureFactory.ExtractECDSASignature(sig);
        var recoveredKey = EthECKey.RecoverFromSignature(ethSig, digest);
        var recovered = recoveredKey.GetPublicAddress();
        Assert.Equal("0x70997970C51812dc3A010C7d01b50e0d17dc79C8".ToLowerInvariant(), recovered.ToLowerInvariant());
    }

    [Fact]
    public void SignTournamentResult_SyncOverload_WorksIdentically()
    {
        var client = AmpTournamentClient.FromPrivateKey(VerifierKey);
        var sig = client.SignTournamentResult(42, TestWinners, VerifierKey);
        Assert.Equal(
            "0x997d2719534309ae958f42226044f4ab79ac4bf3635fd9ce672035f129a24bb0471b63cab948421595c849ea09d7f8df84bed0cfd8dddaf93f04a4ed5d88473c1c",
            sig.ToLowerInvariant());
    }
}

// ═══════════════════════════════════════════════════════════════
// CLIENT CONFIGURATION & ERROR HANDLING TESTS
// ═══════════════════════════════════════════════════════════════

public class TournamentClientConfigTests
{
    private const string DummyKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

    [Fact]
    public void Client_InitializesWithDefaultFujiSettings()
    {
        var client = new AmpTournamentClient();
        Assert.Equal(AmpTournamentClient.DefaultRpcUrl, client.RpcUrl);
        Assert.Equal(AmpTournamentClient.DefaultContractAddress, client.ContractAddress);
        Assert.Equal(43113, client.ChainId);
        Assert.Null(client.Signer);
    }

    [Fact]
    public void Client_InitializesWithCustomSettings()
    {
        var customContract = "0x1111111111111111111111111111111111111111";
        var client = new AmpTournamentClient(
            rpcUrl: "http://127.0.0.1:8545",
            contractAddress: customContract,
            chainId: 1337
        );
        Assert.Equal("http://127.0.0.1:8545", client.RpcUrl);
        Assert.Equal(customContract, client.ContractAddress);
        Assert.Equal(1337, client.ChainId);
    }

    [Fact]
    public async Task Client_FromPrivateKey_InitializesSigner()
    {
        var client = AmpTournamentClient.FromPrivateKey(DummyKey);
        Assert.NotNull(client.Signer);
        var addr = await client.Signer.GetAddress();
        Assert.Equal("0x70997970c51812dc3a010c7d01b50e0d17dc79c8", addr.ToLowerInvariant());
    }

    [Fact]
    public async Task ClaimPrize_WithoutSigner_ThrowsInvalidOperationException()
    {
        var client = new AmpTournamentClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ClaimPrizeAsync(1, 0));
    }

    [Fact]
    public async Task CanClaimPrize_EmptyAddress_ThrowsArgumentException()
    {
        var client = new AmpTournamentClient();
        await Assert.ThrowsAsync<ArgumentException>(() => client.CanClaimPrizeAsync(1, ""));
    }

    [Fact]
    public void Models_ToString_FormatProperly()
    {
        var t = new TournamentData
        {
            Id = 5,
            PrizePoolWei = BigInteger.Parse("1000000000000000000"),
            State = TournamentState.Finalized,
            Winners = new[] { "0x123", "0x456" }
        };
        var str = t.ToString();
        Assert.Contains("Tournament #5", str);
        Assert.Contains("Finalized", str);

        var canClaim = new CanClaimResult
        {
            CanClaim = true,
            IsWinner = true,
            Placement = 0,
            EstimatedPrizeAvax = 0.7m
        };
        Assert.Contains("Winner!", canClaim.ToString());
    }
}

// ═══════════════════════════════════════════════════════════════
// LIVE INTEGRATION TESTS (Avalanche Fuji Testnet)
// ═══════════════════════════════════════════════════════════════

[Collection("FujiLive")]
public class FujiTournamentLiveTests
{
    private readonly AmpTournamentClient _client = new();

    [Fact]
    public async Task Live_GetTournament_Tournament1_MatchesOnChainState()
    {
        var t = await _client.GetTournamentAsync(1);

        Assert.Equal(new BigInteger(1), t.Id);
        Assert.Equal("0x95cc495df579981d3ffa4a8f77b93a17563e077a", t.Sponsor.ToLowerInvariant());
        Assert.True(t.IsNativeAvax);
        Assert.True(t.PrizePoolWei > 0, "PrizePoolWei must be positive");
        Assert.True(t.PrizePoolAvax > 0m, "PrizePoolAvax must be positive");
        Assert.Equal(TournamentState.Complete, t.State);
        Assert.True(t.IsComplete);
        Assert.Equal(2, t.PayoutBps.Length);
        Assert.Equal(7000, t.PayoutBps[0]);
        Assert.Equal(3000, t.PayoutBps[1]);
        Assert.Equal(2, t.Winners.Length);
        Assert.Equal("0x1563915e194D8CfBA1943570603F7606A3115508".ToLowerInvariant(), t.Winners[0].ToLowerInvariant());
        Assert.Equal("0x5CbDd86a2FA8Dc4bDdd8a8f69dBa48572EeC07FB".ToLowerInvariant(), t.Winners[1].ToLowerInvariant());
    }

    [Fact]
    public async Task Live_IsPrizeClaimed_Tournament1_BothClaimed()
    {
        var claimed0 = await _client.IsPrizeClaimedAsync(1, 0);
        var claimed1 = await _client.IsPrizeClaimedAsync(1, 1);

        Assert.True(claimed0, "Placement 0 of Tournament 1 was claimed");
        Assert.True(claimed1, "Placement 1 of Tournament 1 was claimed");
    }

    [Fact]
    public async Task Live_CanClaimPrize_Tournament1_ReportsAlreadyClaimed()
    {
        var winner0 = "0x1563915e194D8CfBA1943570603F7606A3115508";
        var result = await _client.CanClaimPrizeAsync(1, winner0);

        Assert.False(result.CanClaim);
        Assert.True(result.IsWinner);
        Assert.Equal(0, result.Placement);
        Assert.True(result.IsClaimed);
        Assert.True(result.AlreadyClaimed);
        Assert.Contains("already been claimed", result.Reason);
    }

    [Fact]
    public async Task Live_CanClaimPrize_RejectsNonWinner()
    {
        var nonWinner = "0x0000000000000000000000000000000000000001";
        var result = await _client.CanClaimPrizeAsync(1, nonWinner);

        Assert.False(result.CanClaim);
        Assert.False(result.IsWinner);
        Assert.Equal(-1, result.Placement);
        Assert.False(result.IsClaimed);
    }

    [Fact]
    public async Task Live_GetTournament_Tournament8_FinalizedWithUnclaimedPrizes()
    {
        var t = await _client.GetTournamentAsync(8);

        Assert.Equal(new BigInteger(8), t.Id);
        Assert.Equal(TournamentState.Finalized, t.State);
        Assert.True(t.IsFinalized);
        Assert.Equal(1.0m, t.PrizePoolAvax);
        Assert.Equal(3, t.Winners.Length);

        // Winner 0 has not yet claimed in Tournament 8
        var claimed0 = await _client.IsPrizeClaimedAsync(8, 0);
        Assert.False(claimed0);

        var winner0 = t.Winners[0];
        var claimResult = await _client.CanClaimPrizeAsync(8, winner0);
        Assert.True(claimResult.CanClaim);
        Assert.True(claimResult.IsWinner);
        Assert.Equal(0, claimResult.Placement);
        Assert.False(claimResult.IsClaimed);
        Assert.Equal(0.6m, claimResult.EstimatedPrizeAvax);
    }

    [Fact]
    public async Task Live_DomainSeparator_MatchesContract()
    {
        var ds = await _client.GetDomainSeparatorAsync();
        Assert.Equal("0xa06f97ad3d7e36a974ca10b74652528a5c2708ac10140a16de0f3080edf8ccd6", ds.ToLowerInvariant());
    }

    [Fact]
    public async Task Live_NextTournamentId_ReturnsValidCounter()
    {
        var nextId = await _client.GetNextTournamentIdAsync();
        Assert.True(nextId >= 10, $"nextTournamentId was {nextId}");
    }
}
