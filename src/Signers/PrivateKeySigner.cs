using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Numerics;
using System.Text;

namespace Amp.Sdk.Signers;

/// <summary>
/// Signer backed by a raw private key. For server-side games and testing.
/// Uses Nethereum for EIP-191 (personal_sign) and manual EIP-712 digest
/// computation for typed-data signing.
///
/// IMPORTANT: Never expose private keys in client-side code.
/// </summary>
public class PrivateKeySigner : IAMPSigner
{
    private readonly EthECKey _key;
    private readonly string _address;
    private static readonly Sha3Keccack Keccak = new();

    public PrivateKeySigner(string privateKey)
    {
        _key = new EthECKey(privateKey.EnsureHexPrefix());
        _address = _key.GetPublicAddress();
    }

    public Task<string> GetAddress() => Task.FromResult(_address);

    public Task<string> SignPersonalSign(string message)
    {
        var signer = new EthereumMessageSigner();
        var signature = signer.EncodeUTF8AndSign(message, _key);
        return Task.FromResult(signature.EnsureHexPrefix());
    }

    public Task<string> SignTypedData(Eip712TypedData typedData)
    {
        var digest = ComputeEip712Digest(typedData);
        var ethSig = _key.SignAndCalculateV(digest);

        // Construct 65-byte signature: r (32) + s (32) + v (1)
        var sig = new byte[65];

        // R and S are byte[] — right-align in the 32-byte slots
        Array.Copy(ethSig.R, 0, sig, 32 - Math.Min(ethSig.R.Length, 32), Math.Min(ethSig.R.Length, 32));
        Array.Copy(ethSig.S, 0, sig, 64 - Math.Min(ethSig.S.Length, 32), Math.Min(ethSig.S.Length, 32));

        // V is byte[] — normalize to Ethereum convention (27 or 28)
        var v = ethSig.V[0];
        sig[64] = v >= 27 ? v : (byte)(v + 27);

        return Task.FromResult("0x" + sig.ToHex(false).ToLowerInvariant());
    }

    /// <summary>
    /// EIP-712 digest (domain separator ‖ hashStruct). Pure — exposed for
    /// cross-SDK conformance testing against the ethers golden vector.
    /// </summary>
    public static byte[] ComputeEip712Digest(Eip712TypedData td)
    {
        var domainSep = ComputeDomainSeparator(td);
        var structHash = ComputeStructHash(td);

        var input = new byte[66];
        input[0] = 0x19;
        input[1] = 0x01;
        Array.Copy(domainSep, 0, input, 2, 32);
        Array.Copy(structHash, 0, input, 34, 32);
        return Keccak.CalculateHash(input);
    }

    private static byte[] ComputeDomainSeparator(Eip712TypedData td)
    {
        var typeHash = Keccak.CalculateHash(Encoding.UTF8.GetBytes(
            "EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)"));
        var nameHash = Keccak.CalculateHash(Encoding.UTF8.GetBytes(td.Name));
        var versionHash = Keccak.CalculateHash(Encoding.UTF8.GetBytes(td.Version));
        var chainIdWord = BigIntegerToWord32(td.ChainId);
        var contractWord = PadLeft32(HexToBytes(td.VerifyingContract));

        var input = new byte[160];
        Array.Copy(typeHash, 0, input, 0, 32);
        Array.Copy(nameHash, 0, input, 32, 32);
        Array.Copy(versionHash, 0, input, 64, 32);
        Array.Copy(chainIdWord, 0, input, 96, 32);
        Array.Copy(contractWord, 0, input, 128, 32);
        return Keccak.CalculateHash(input);
    }

    private static byte[] ComputeStructHash(Eip712TypedData td)
    {
        var fields = td.Types[td.PrimaryType];
        var typeString = $"{td.PrimaryType}({string.Join(",", fields.Select(f => $"{f.Type} {f.Name}"))})";
        var typeHash = Keccak.CalculateHash(Encoding.UTF8.GetBytes(typeString));

        var input = new byte[32 * (fields.Length + 1)];
        Array.Copy(typeHash, 0, input, 0, 32);
        for (var i = 0; i < fields.Length; i++)
        {
            var valueBytes = EncodeFieldValue(fields[i], td.Message);
            Array.Copy(valueBytes, 0, input, 32 * (i + 1), 32);
        }
        return Keccak.CalculateHash(input);
    }

    private static byte[] EncodeFieldValue(TypedDataField field, Dictionary<string, object> message)
    {
        if (!message.TryGetValue(field.Name, out var value) || value == null)
            return new byte[32];

        switch (field.Type)
        {
            case "string":
                return Keccak.CalculateHash(Encoding.UTF8.GetBytes(value.ToString() ?? ""));

            case "bytes32":
                return PadRight32(HexToBytes(value.ToString() ?? "0x00"));

            case "uint256":
            case "uint":
                var bigInt = ParseBigInteger(value);
                return BigIntegerToWord32(bigInt);

            case "address":
                return PadLeft32(HexToBytes(value.ToString() ?? ""));

            default:
                if (field.Type.EndsWith("[]"))
                    return EncodeArrayField(field, value);
                return new byte[32];
        }
    }

    private static byte[] EncodeArrayField(TypedDataField field, object value)
    {
        var elementType = field.Type[..^2];

        List<object> elements = value switch
        {
            string[] arr => arr.Select(s => (object)s).ToList(),
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Array
                => je.EnumerateArray().Select(e => (object)e).ToList(),
            List<string> list => list.Select(s => (object)s).ToList(),
            _ => new List<object>(),
        };

        var concat = new byte[elements.Count * 32];
        for (var i = 0; i < elements.Count; i++)
        {
            var elemField = new TypedDataField("elem", elementType);
            var encoded = EncodeFieldValue(elemField, new Dictionary<string, object> { ["elem"] = elements[i] });
            Array.Copy(encoded, 0, concat, i * 32, 32);
        }
        return Keccak.CalculateHash(concat);
    }

    private static BigInteger ParseBigInteger(object value)
    {
        return value switch
        {
            long l => l,
            int i => i,
            BigInteger bi => bi,
            string s when BigInteger.TryParse(s, out var parsed) => parsed,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number
                => BigInteger.Parse(je.GetRawText()),
            _ => BigInteger.Zero,
        };
    }

    /// <summary>BigInteger as a right-aligned 32-byte big-endian word.</summary>
    private static byte[] BigIntegerToWord32(BigInteger value)
    {
        if (value.Sign < 0) value = BigInteger.Zero;
        var bytes = value.ToByteArray(); // little-endian, minimal length
        if (bytes.Length > 32) bytes = bytes[^32..];
        var word = new byte[32];
        // Right-align: reverse the little-endian bytes into word[32-len .. 31]
        for (var i = 0; i < bytes.Length; i++)
            word[32 - bytes.Length + i] = bytes[bytes.Length - 1 - i];
        return word;
    }

    private static byte[] PadLeft32(byte[] input)
    {
        var result = new byte[32];
        var bytes = input.Length > 32 ? input[^32..] : input;
        Array.Copy(bytes, 0, result, 32 - bytes.Length, bytes.Length);
        return result;
    }

    private static byte[] PadRight32(byte[] input)
    {
        var result = new byte[32];
        var bytes = input.Length > 32 ? input[..32] : input;
        Array.Copy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace("0x", "").Replace("0X", "");
        if (hex.Length % 2 != 0) hex = "0" + hex;
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}
