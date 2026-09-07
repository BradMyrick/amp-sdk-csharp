namespace Amp.Sdk;

/// <summary>
/// Core signer interfaces — the dual-path wallet model.
/// Use AMPSigner for self-custody (player's wallet) or
/// AMPCustodialProvider for fiat-friendly custodial games.
/// </summary>

public record TypedDataField(string Name, string Type);

public record TypedDataType(params TypedDataField[] Fields)
{
    public string Name { get; init; } = "";
}

public record TypedDataMessage(Dictionary<string, object> Data);

public record Eip712TypedData(
    string Name,
    string Version,
    long ChainId,
    string VerifyingContract,
    string PrimaryType,
    Dictionary<string, TypedDataField[]> Types,
    Dictionary<string, object> Message
);

/// <summary>
/// Self-custody signer — the player signs with their own wallet.
/// Implement this with Nethereum, or use the built-in PrivateKeySigner.
/// </summary>
public interface IAMPSigner
{
    Task<string> GetAddress();
    Task<string> SignPersonalSign(string message);
    Task<string> SignTypedData(Eip712TypedData typedData);
}

/// <summary>
/// Custodial provider — the game studio handles fiat on/off-ramping.
/// AMP's smart contracts still handle escrow and settlement.
/// </summary>
public interface IAMPCustodialProvider
{
    Task<string> GetAddress(string playerId);
    Task<string> SignPersonalSign(string playerId, string message);
    Task<string> SignTypedData(string playerId, Eip712TypedData typedData);
    Task FundMatch(string matchId, string playerId);
    Task WithdrawWinnings(string matchId, string playerId);
}
