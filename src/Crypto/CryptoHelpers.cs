using System.Security.Cryptography;
using System.Text;

namespace Amp.Sdk.Crypto;

/// <summary>
/// Crypto helpers — commit hashes, EIP-712 typed data construction,
/// and EIP-191 report messages.
/// </summary>
public static class CryptoHelpers
{
    /// <summary>
    /// Generate a cryptographically random salt for commit-reveal.
    /// Returns 32 bytes as a hex string (0x-prefixed).
    /// </summary>
    public static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "0x" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Build the EIP-191 message for a 1v1 match report.
    /// Matches the amp-server's report_message function.
    /// </summary>
    public static string BuildReportMessage(string matchId, string result)
    {
        return $"AMP_REPORT:v1:{matchId}:{result}";
    }

    /// <summary>
    /// Build EIP-712 typed data for a MultiplayerLadder signature.
    /// Matches the amp-server's ladder.rs and AMPMultiplayer contract.
    /// </summary>
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

    /// <summary>
    /// Convert a UTF-8 string to hex (for personal_sign params).
    /// </summary>
    public static string ToHex(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        return "0x" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Zero-pad a value to 32 bytes (for matchId/gameId bytes32 fields).
    /// </summary>
    public static string ToBytes32(long value)
    {
        var bytes = new byte[32];
        var valueBytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(valueBytes);
        Array.Copy(valueBytes, 0, bytes, 32 - valueBytes.Length, valueBytes.Length);
        return "0x" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
