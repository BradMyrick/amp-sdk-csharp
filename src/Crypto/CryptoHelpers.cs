using System.Security.Cryptography;
using System.Text;

namespace Amp.Sdk.Crypto;

/// <summary>
/// Crypto helpers — commit hashes, EIP-712 typed data construction,
/// and EIP-191 report messages.
/// </summary>
public static class CryptoHelpers
{
    /// <summary>Generate a cryptographically random salt (32 bytes, 0x-prefixed hex).</summary>
    public static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "0x" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Build the EIP-191 message for a 1v1 match report.</summary>
    public static string BuildReportMessage(string matchId, string result)
        => $"AMP_REPORT:v1:{matchId}:{result}";

    /// <summary>
    /// Compute the commit-reveal hash: keccak256(address ‖ stake ‖ salt).
    /// Uses Nethereum's Sha3Keccack for the hash and address encoding.
    /// </summary>
    public static async Task<string> ComputeCommitHashAsync(
        string wallet, long stakeWei, string salt)
    {
        var keccak = new Nethereum.Util.Sha3Keccack();

        // Address → 20 bytes
        var addrHex = wallet.Replace("0x", "").Replace("0X", "");
        var addrBytes = new byte[20];
        for (var i = 0; i < 20; i++)
            addrBytes[i] = Convert.ToByte(addrHex.Substring(i * 2, 2), 16);

        // Stake → 8 bytes big-endian
        var stakeBytes = BitConverter.GetBytes(stakeWei).Reverse().ToArray();

        // Salt → UTF-8 bytes
        var saltBytes = Encoding.UTF8.GetBytes(salt);

        // Concatenate and hash
        var input = new byte[addrBytes.Length + stakeBytes.Length + saltBytes.Length];
        Array.Copy(addrBytes, 0, input, 0, addrBytes.Length);
        Array.Copy(stakeBytes, 0, input, addrBytes.Length, stakeBytes.Length);
        Array.Copy(saltBytes, 0, input, addrBytes.Length + stakeBytes.Length, saltBytes.Length);

        var hash = keccak.CalculateHash(input);
        return "0x" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Build EIP-712 typed data for a MultiplayerLadder signature.</summary>
    public static Eip712TypedData BuildLadderTypedData(
        long chainId,
        string contractAddress,
        string matchId,
        string gameId,
        string[] rankedPlacements,
        string transcriptHash,
        long sessionNonce)
    {
        return new Eip712TypedData(
            Name: "AMPMultiplayer",
            Version: "1",
            ChainId: chainId,
            VerifyingContract: contractAddress,
            PrimaryType: "MultiplayerLadder",
            Types: new Dictionary<string, TypedDataField[]>
            {
                ["MultiplayerLadder"] = new[]
                {
                    new TypedDataField("matchId", "bytes32"),
                    new TypedDataField("gameId", "bytes32"),
                    new TypedDataField("rankedPlacements", "address[]"),
                    new TypedDataField("transcriptHash", "bytes32"),
                    new TypedDataField("sessionNonce", "uint256"),
                },
            },
            Message: new Dictionary<string, object>
            {
                ["matchId"] = matchId,
                ["gameId"] = gameId,
                ["rankedPlacements"] = rankedPlacements,
                ["transcriptHash"] = transcriptHash,
                ["sessionNonce"] = sessionNonce,
            }
        );
    }

    /// <summary>Convert a UTF-8 string to hex (for personal_sign params).</summary>
    public static string ToHex(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        return "0x" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Zero-pad a long value to a 32-byte hex string (bytes32).</summary>
    public static string ToBytes32(long value)
    {
        var bytes = new byte[32];
        var valueBytes = BitConverter.GetBytes(value).Reverse().ToArray();
        Array.Copy(valueBytes, 0, bytes, 32 - valueBytes.Length, valueBytes.Length);
        return "0x" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
