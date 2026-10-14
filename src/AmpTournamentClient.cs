using System.Numerics;
using Amp.Sdk.Crypto;
using Amp.Sdk.Signers;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace Amp.Sdk;

/// <summary>
/// Client for the Avalanche Tournament & Match Protocol (AMPTournamentCup).
/// Manages tournament prize pools, queries winners, verifies pull-claims,
/// signs EIP-712 attestations, and broadcasts claim transactions on Avalanche Fuji and C-Chain.
/// </summary>
public class AmpTournamentClient
{
    public const string DefaultRpcUrl = "https://api.avax-test.network/ext/bc/C/rpc";
    public const string DefaultContractAddress = "0x7C743c1C9AE3e7A65d030098F2249B7787d66dfF";
    public const long DefaultChainId = 43113;

    // Function Selectors on AMPTournamentCup
    public const string SigGetTournament = "0x1a5bd7fc";   // getTournament(uint256)
    public const string SigClaimed = "0xa54ab457";         // claimed(uint256,uint256)
    public const string SigClaimPrize = "0x7b154140";       // claimPrize(uint256,uint256)
    public const string SigNextTournamentId = "0x852efc3d"; // nextTournamentId()
    public const string SigDomainSeparator = "0xf698da25";  // domainSeparator()
    public const string SigProtocolFeeBps = "0x9859f518";   // protocolFeeBps()

    private static readonly AddressUtil _addressUtil = new();

    public string RpcUrl { get; }
    public string ContractAddress { get; }
    public long ChainId { get; }
    public IAMPSigner? Signer { get; }
    public IWeb3 Web3 { get; }

    public AmpTournamentClient()
        : this((string?)null)
    {
    }

    public AmpTournamentClient(
        IAMPSigner signer,
        string? rpcUrl = null,
        string? contractAddress = null,
        long chainId = DefaultChainId)
        : this(rpcUrl, contractAddress, chainId, signer)
    {
    }

    public AmpTournamentClient(
        string? rpcUrl,
        string? contractAddress = null,
        long chainId = DefaultChainId,
        IAMPSigner? signer = null,
        string? privateKey = null)
    {
        RpcUrl = string.IsNullOrWhiteSpace(rpcUrl) ? DefaultRpcUrl : rpcUrl.TrimEnd('/');
        ContractAddress = string.IsNullOrWhiteSpace(contractAddress) ? DefaultContractAddress : contractAddress;
        ChainId = chainId > 0 ? chainId : DefaultChainId;

        if (signer == null && !string.IsNullOrWhiteSpace(privateKey))
        {
            signer = new PrivateKeySigner(privateKey);
        }

        Signer = signer;

        if (signer is PrivateKeySigner pks && !string.IsNullOrEmpty(pks.PrivateKeyHex))
        {
            var account = new Account(pks.PrivateKeyHex, ChainId);
            Web3 = new Web3(account, RpcUrl);
        }
        else
        {
            Web3 = new Web3(RpcUrl);
        }
    }

    public static AmpTournamentClient FromPrivateKey(
        string privateKey,
        string? rpcUrl = null,
        string? contractAddress = null,
        long chainId = DefaultChainId) =>
        new(rpcUrl, contractAddress, chainId, privateKey: privateKey);

    public AmpTournamentClient(
        IWeb3 web3,
        string? contractAddress = null,
        long chainId = DefaultChainId,
        IAMPSigner? signer = null)
    {
        Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        RpcUrl = DefaultRpcUrl;
        ContractAddress = string.IsNullOrWhiteSpace(contractAddress) ? DefaultContractAddress : contractAddress;
        ChainId = chainId > 0 ? chainId : DefaultChainId;
        Signer = signer;
    }

    // ── High-Level Tournament API ──────────────────────────────

    /// <summary>Fetch all tournament details from the on-chain contract.</summary>
    public async Task<TournamentData> GetTournamentAsync(BigInteger tournamentId)
    {
        var calldata = EncodeGetTournament(tournamentId);
        var callInput = new CallInput
        {
            To = ContractAddress,
            Data = calldata
        };
        var rawHex = await Web3.Eth.Transactions.Call.SendRequestAsync(callInput);
        return DecodeTournament(tournamentId, rawHex);
    }

    /// <summary>Check if a specific placement's prize has already been claimed.</summary>
    public async Task<bool> IsPrizeClaimedAsync(BigInteger tournamentId, BigInteger placement)
    {
        var calldata = EncodeClaimed(tournamentId, placement);
        var callInput = new CallInput
        {
            To = ContractAddress,
            Data = calldata
        };
        var rawHex = await Web3.Eth.Transactions.Call.SendRequestAsync(callInput);
        return DecodeBool(rawHex);
    }

    /// <summary>
    /// Evaluates whether a player won a prize in the tournament, their placement,
    /// and whether they can claim now.
    /// </summary>
    public async Task<CanClaimResult> CanClaimPrizeAsync(BigInteger tournamentId, string walletAddress)
    {
        if (string.IsNullOrWhiteSpace(walletAddress))
            throw new ArgumentException("Wallet address cannot be null or empty.", nameof(walletAddress));

        var tournament = await GetTournamentAsync(tournamentId);
        var placement = -1;

        if (tournament.Winners != null)
        {
            for (var i = 0; i < tournament.Winners.Length; i++)
            {
                if (string.Equals(tournament.Winners[i], walletAddress, StringComparison.OrdinalIgnoreCase))
                {
                    placement = i;
                    break;
                }
            }
        }

        if (placement < 0)
        {
            return new CanClaimResult
            {
                CanClaim = false,
                IsWinner = false,
                Placement = -1,
                EstimatedPrizeWei = BigInteger.Zero,
                EstimatedPrizeAvax = 0m,
                IsClaimed = false,
                Reason = $"Address {walletAddress} is not an attested winner in tournament #{tournamentId}",
                Tournament = tournament
            };
        }

        var isClaimed = await IsPrizeClaimedAsync(tournamentId, placement);
        var estimatedPrizeWei = tournament.GetPlacementPrizeWei(placement);
        var estimatedPrizeAvax = tournament.GetPlacementPrizeAvax(placement);

        if (isClaimed)
        {
            return new CanClaimResult
            {
                CanClaim = false,
                IsWinner = true,
                Placement = placement,
                EstimatedPrizeWei = estimatedPrizeWei,
                EstimatedPrizeAvax = estimatedPrizeAvax,
                IsClaimed = true,
                Reason = $"Prize for placement #{placement + 1} has already been claimed",
                Tournament = tournament
            };
        }

        if (tournament.State != TournamentState.Finalized && tournament.State != TournamentState.Complete)
        {
            return new CanClaimResult
            {
                CanClaim = false,
                IsWinner = true,
                Placement = placement,
                EstimatedPrizeWei = estimatedPrizeWei,
                EstimatedPrizeAvax = estimatedPrizeAvax,
                IsClaimed = false,
                Reason = $"Tournament state is {tournament.State}; claims only open when Finalized",
                Tournament = tournament
            };
        }

        return new CanClaimResult
        {
            CanClaim = true,
            IsWinner = true,
            Placement = placement,
            EstimatedPrizeWei = estimatedPrizeWei,
            EstimatedPrizeAvax = estimatedPrizeAvax,
            IsClaimed = false,
            Reason = "Eligible to claim",
            Tournament = tournament
        };
    }

    /// <summary>
    /// Executes the on-chain claimPrize transaction if a signer or account is configured.
    /// </summary>
    public async Task<string> ClaimPrizeAsync(BigInteger tournamentId, BigInteger placement)
    {
        var account = Web3.TransactionManager?.Account;
        if (account == null)
        {
            if (Signer is PrivateKeySigner pks && !string.IsNullOrEmpty(pks.PrivateKeyHex))
            {
                account = new Account(pks.PrivateKeyHex, ChainId);
            }
            else
            {
                throw new InvalidOperationException(
                    "No signer configured for transaction execution. Provide a privateKey or configure an account.");
            }
        }

        var calldata = EncodeClaimPrize(tournamentId, placement);
        var txInput = new TransactionInput
        {
            To = ContractAddress,
            From = account.Address,
            Data = calldata,
            Value = new HexBigInteger(0)
        };
        return await Web3.Eth.Transactions.SendTransaction.SendRequestAsync(txInput);
    }

    /// <summary>
    /// Produces an EIP-712 attestation signature over TournamentResult(uint256 tournamentId,address[] winners).
    /// </summary>
    public async Task<string> SignTournamentResultAsync(BigInteger tournamentId, string[] winners, IAMPSigner? signer = null)
    {
        var activeSigner = signer ?? Signer;
        if (activeSigner == null)
            throw new InvalidOperationException("No signer configured for EIP-712 signing.");

        var typedData = BuildTournamentResultTypedData(ChainId, ContractAddress, tournamentId, winners);
        return await activeSigner.SignTypedData(typedData);
    }

    public Task<string> SignTournamentResultAsync(BigInteger tournamentId, string[] winners, string privateKey)
    {
        var pks = new PrivateKeySigner(privateKey);
        return SignTournamentResultAsync(tournamentId, winners, pks);
    }

    public string SignTournamentResult(BigInteger tournamentId, string[] winners, IAMPSigner? signer = null)
    {
        return SignTournamentResultAsync(tournamentId, winners, signer).GetAwaiter().GetResult();
    }

    public string SignTournamentResult(BigInteger tournamentId, string[] winners, string privateKey)
    {
        return SignTournamentResultAsync(tournamentId, winners, privateKey).GetAwaiter().GetResult();
    }

    /// <summary>Compute the 32-byte EIP-712 digest for this tournament result.</summary>
    public byte[] ComputeTournamentResultDigest(BigInteger tournamentId, string[] winners)
    {
        var typedData = BuildTournamentResultTypedData(ChainId, ContractAddress, tournamentId, winners);
        return PrivateKeySigner.ComputeEip712Digest(typedData);
    }

    /// <summary>Returns the next tournament ID counter from the contract.</summary>
    public async Task<BigInteger> GetNextTournamentIdAsync()
    {
        var callInput = new CallInput
        {
            To = ContractAddress,
            Data = SigNextTournamentId
        };
        var rawHex = await Web3.Eth.Transactions.Call.SendRequestAsync(callInput);
        return HexToBigInteger(rawHex);
    }

    /// <summary>Returns the EIP-712 domain separator from the contract.</summary>
    public async Task<string> GetDomainSeparatorAsync()
    {
        var callInput = new CallInput
        {
            To = ContractAddress,
            Data = SigDomainSeparator
        };
        return await Web3.Eth.Transactions.Call.SendRequestAsync(callInput);
    }

    // ── ABI Encoders & Decoders ────────────────────────────────

    public static string EncodeGetTournament(BigInteger tournamentId) =>
        SigGetTournament + BigIntegerToHex32(tournamentId);

    public static string EncodeClaimed(BigInteger tournamentId, BigInteger placement) =>
        SigClaimed + BigIntegerToHex32(tournamentId) + BigIntegerToHex32(placement);

    public static string EncodeClaimPrize(BigInteger tournamentId, BigInteger placement) =>
        SigClaimPrize + BigIntegerToHex32(tournamentId) + BigIntegerToHex32(placement);

    public static TournamentData DecodeTournament(BigInteger tournamentId, string rawHex)
    {
        var raw = HexToBytes(rawHex);
        if (raw == null || raw.Length < 32 * 9)
            throw new ArgumentException("Invalid getTournament return data: payload too short", nameof(rawHex));

        var structStart = (int)ReadUInt256AsULong(raw, 0);
        if (structStart < 0 || structStart + 32 * 9 > raw.Length)
            structStart = 32;

        var sponsor = ReadAddress(raw, structStart + 0);
        var token = ReadAddress(raw, structStart + 32);
        var prizePool = ReadUInt256(raw, structStart + 64);
        var payoutBpsOffset = structStart + (int)ReadUInt256AsULong(raw, structStart + 96);
        var verifier = ReadAddress(raw, structStart + 128);
        var winnersOffset = structStart + (int)ReadUInt256AsULong(raw, structStart + 160);
        var stateByte = raw[structStart + 192 + 31];
        var createdAt = ReadUInt256AsULong(raw, structStart + 224);
        var finalizeDeadline = ReadUInt256AsULong(raw, structStart + 256);

        var payoutLength = (int)ReadUInt256AsULong(raw, payoutBpsOffset);
        var payoutBps = new int[payoutLength];
        for (var i = 0; i < payoutLength; i++)
            payoutBps[i] = (int)ReadUInt256AsULong(raw, payoutBpsOffset + 32 + i * 32);

        var winnersLength = (int)ReadUInt256AsULong(raw, winnersOffset);
        var winners = new string[winnersLength];
        for (var i = 0; i < winnersLength; i++)
            winners[i] = ReadAddress(raw, winnersOffset + 32 + i * 32);

        return new TournamentData
        {
            Id = tournamentId,
            Sponsor = sponsor,
            Token = token,
            PrizePoolWei = prizePool,
            PayoutBps = payoutBps,
            Verifier = verifier,
            Winners = winners,
            State = (TournamentState)stateByte,
            CreatedAt = new TournamentTimestamp(createdAt),
            FinalizeDeadline = new TournamentTimestamp(finalizeDeadline)
        };
    }

    public static bool DecodeBool(string rawHex)
    {
        var bytes = HexToBytes(rawHex);
        if (bytes == null || bytes.Length == 0) return false;
        return bytes[^1] != 0;
    }

    public static Eip712TypedData BuildTournamentResultTypedData(
        long chainId,
        string contractAddress,
        BigInteger tournamentId,
        string[] winners)
    {
        return CryptoHelpers.BuildTournamentResultTypedData(chainId, contractAddress, tournamentId, winners);
    }

    public static string BigIntegerToHex32(BigInteger value)
    {
        if (value.Sign < 0) value = BigInteger.Zero;
        var bytes = value.ToByteArray(); // little-endian
        var len = bytes.Length;
        while (len > 0 && bytes[len - 1] == 0) len--; // strip trailing zero sign bytes
        var word = new byte[32];
        for (var i = 0; i < len && i < 32; i++)
            word[31 - i] = bytes[i];
        return Convert.ToHexString(word).ToLowerInvariant();
    }

    public static byte[] HexToBytes(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Array.Empty<byte>();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];
        if (hex.Length % 2 != 0) hex = "0" + hex;
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    public static BigInteger HexToBigInteger(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return BigInteger.Zero;
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex[2..];
        if (string.IsNullOrEmpty(hex)) return BigInteger.Zero;
        return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
    }

    private static BigInteger ReadUInt256(byte[] data, int offset)
    {
        var slice = new byte[32];
        Array.Copy(data, offset, slice, 0, 32);
        Array.Reverse(slice);
        var unsigned = new byte[33];
        Array.Copy(slice, 0, unsigned, 0, 32);
        return new BigInteger(unsigned);
    }

    private static ulong ReadUInt256AsULong(byte[] data, int offset)
    {
        ulong val = 0;
        for (var i = 24; i < 32; i++)
            val = (val << 8) | data[offset + i];
        return val;
    }

    private static string ReadAddress(byte[] data, int offset)
    {
        var addr = new byte[20];
        Array.Copy(data, offset + 12, addr, 0, 20);
        var hex = "0x" + Convert.ToHexString(addr).ToLowerInvariant();
        try
        {
            return _addressUtil.ConvertToChecksumAddress(hex);
        }
        catch
        {
            return hex;
        }
    }
}
