using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Amp.Sdk.Signers;

/// <summary>
/// Signer backed by a raw private key. For server-side games and testing.
/// Uses Nethereum for EIP-191 signing.
///
/// EIP-712 typed-data signing (for N-player ladders) is supported via
/// the SignTypedDataAsync method — implement IAMPSigner directly if you
/// need EIP-712 before official support lands.
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
        var signature = signer.EncodeUTF8AndSign(message, _key);
        return Task.FromResult(signature.EnsureHexPrefix());
    }

    /// <summary>
    /// EIP-712 typed-data signing for multiplayer ladders.
    /// Currently throws — implement IAMPSigner with Nethereum's EIP-712
    /// module for multiplayer support, or wait for official support.
    /// </summary>
    public Task<string> SignTypedData(Eip712TypedData typedData)
    {
        throw new NotSupportedException(
            "EIP-712 signing not yet implemented in the built-in signer. " +
            "For N-player multiplayer, implement IAMPSigner.SignTypedData " +
            "using Nethereum.Signer.EIP712 or a web3 provider. " +
            "1v1 matches (login, queue, report) work fully with this signer.");
    }
}
