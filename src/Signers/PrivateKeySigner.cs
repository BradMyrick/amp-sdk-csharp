using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Amp.Sdk.Signers;

/// <summary>
/// Signer backed by a raw private key. For server-side games and testing.
/// Uses Nethereum internally for EIP-191 and EIP-712 signing.
///
/// IMPORTANT: Never expose private keys in client-side code.
/// </summary>
public class PrivateKeySigner : IAMPSigner
{
    private readonly EthECKey _key;
    private readonly string _address;

    public PrivateKeySigner(string privateKey)
    {
        _key = new EthECKey(privateKey.EnsureHexPrefix());
        _address = _key.GetPublicAddress();
    }

    public Task<string> GetAddress() => Task.FromResult(_address);

    public Task<string> SignPersonalSign(string message)
    {
        var signer = new EthereumMessageSigner();
        var signature = signer.Sign(message, _key);
        return Task.FromResult(signature.EnsureHexPrefix());
    }

    public Task<string> SignTypedData(Eip712TypedData typedData)
    {
        // Nethereum's EIP-712 signing via TypedDataSmartContractSigning
        var typedDataJson = SerializeTypedData(typedData);
        var signer = new TypedDataSmartContractSigning();
        var signature = signer.SignTypedData(typedDataJson, _key);
        return Task.FromResult(signature.EnsureHexPrefix());
    }

    private static string SerializeTypedData(Eip712TypedData td)
    {
        var domain = new Dictionary<string, object>
        {
            ["name"] = td.Name,
            ["version"] = td.Version,
            ["chainId"] = td.ChainId,
            ["verifyingContract"] = td.VerifyingContract,
        };

        var types = new Dictionary<string, object>();
        foreach (var kvp in td.Types)
        {
            types[kvp.Key] = kvp.Value.Select(f =>
                (object)new Dictionary<string, string> { ["name"] = f.Name, ["type"] = f.Type }
            ).ToArray();
        }
        // EIP-712 requires EIP712Domain type
        types["EIP712Domain"] = new object[]
        {
            new Dictionary<string, string> { ["name"] = "name", ["type"] = "string" },
            new Dictionary<string, string> { ["name"] = "version", ["type"] = "string" },
            new Dictionary<string, string> { ["name"] = "chainId", ["type"] = "uint256" },
            new Dictionary<string, string> { ["name"] = "verifyingContract", ["type"] = "address" },
        };

        var full = new Dictionary<string, object>
        {
            ["domain"] = domain,
            ["types"] = types,
            ["primaryType"] = td.PrimaryType,
            ["message"] = td.Message,
        };

        return System.Text.Json.JsonSerializer.Serialize(full);
    }
}
