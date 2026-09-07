// Example: Custodial / Fiat-Friendly Integration
//
// For games where players don't have crypto wallets.
// The game studio implements IAMPCustodialProvider to handle
// PayPal ↔ AVAX conversion. AMP handles the smart contract rails.
//
// Legal: AMP = protocol infrastructure (like Stripe rails).
// The game studio is the custodial bridge, not AMP.

using Amp.Sdk;

// ── Implement the custodial provider ─────────────────────────────

public class MyCustodialProvider : IAMPCustodialProvider
{
    // Get/create a delegated wallet for this player
    public async Task<string> GetAddress(string playerId)
    {
        // Your wallet infrastructure (HD wallets, MPC, Fireblocks, etc.)
        var wallet = await Db.GetOrCreateWalletAsync(playerId);
        return wallet.Address;
    }

    // Sign EIP-191 on behalf of the player
    public async Task<string> SignPersonalSign(string playerId, string message)
    {
        var wallet = await Db.GetWalletAsync(playerId);
        return await wallet.SignMessageAsync(message);
    }

    // Sign EIP-712 on behalf of the player
    public async Task<string> SignTypedData(string playerId, Eip712TypedData typedData)
    {
        var wallet = await Db.GetWalletAsync(playerId);
        return await wallet.SignTypedDataAsync(typedData);
    }

    // Fund a staked match (convert PayPal → AVAX, deposit to escrow)
    public async Task FundMatch(string matchId, string playerId)
    {
        var payment = await Db.GetPaymentAsync(playerId);
        if (payment?.Status != "completed")
            throw new InvalidOperationException("Payment required");

        var avax = await ConvertUsdToAvaxAsync(payment.AmountUsd);
        await DepositToEscrowAsync(matchId, avax, playerId);
    }

    // Withdraw winnings (claim on-chain, convert to USD, send via PayPal)
    public async Task WithdrawWinnings(string matchId, string playerId)
    {
        var payout = await ClaimFromEscrowAsync(matchId, playerId);
        var usd = await ConvertAvaxToUsdAsync(payout);
        await SendPayPalPayoutAsync(playerId, usd);
    }
}

// ── Use it in your game ──────────────────────────────────────────

public class MyGameServer
{
    public async Task ConnectPlayer(string playerId)
    {
        // Player doesn't need a crypto wallet — your backend handles it
        var amp = new AMPClient(
            "https://amp.playwithamp.xyz",
            custodial: new MyCustodialProvider(),
            playerId: playerId
        );

        await amp.LoginAsync();

        // Queue, match, report — same API as the self-custody path
        await amp.JoinQueueAsync("amp-tactics", "ranked-1v1");

        amp.On<MatchFound>("match_found", async match =>
        {
            var result = await RunGameAsync(match);
            // This calls MyCustodialProvider.SignPersonalSign internally
            await amp.ReportMatchAsync(match.MatchId, result);
        });

        amp.On<JsonElement>("match_result", async result =>
        {
            if (result.GetProperty("won").GetBoolean())
            {
                // Withdraw winnings back to PayPal
                // await custodial.WithdrawWinnings(matchId, playerId);
            }
        });
    }

    private Task<string> RunGameAsync(MatchFound match)
        => Task.FromResult(new Random().Next(2) == 0 ? "win" : "loss");
}

// ── Placeholder implementations — replace with your backend ─────

static class Db
{
    public static Task<Wallet> GetOrCreateWalletAsync(string id) => throw new NotImplementedException();
    public static Task<Wallet> GetWalletAsync(string id) => throw new NotImplementedException();
    public static Task<Payment?> GetPaymentAsync(string id) => throw new NotImplementedException();
}

record Wallet
{
    public string Address { get; init; } = "";
    public Task<string> SignMessageAsync(string msg) => throw new NotImplementedException();
    public Task<string> SignTypedDataAsync(Eip712TypedData td) => throw new NotImplementedException();
}

record Payment
{
    public decimal AmountUsd { get; init; }
    public string Status { get; init; } = "";
}

static class Exchange
{
    public static Task<decimal> ConvertUsdToAvaxAsync(decimal usd) => throw new NotImplementedException();
    public static Task<decimal> ConvertAvaxToUsdAsync(decimal avax) => throw new NotImplementedException();
    public static Task DepositToEscrowAsync(string matchId, decimal avax, string playerId) => throw new NotImplementedException();
    public static Task<decimal> ClaimFromEscrowAsync(string matchId, string playerId) => throw new NotImplementedException();
    public static Task SendPayPalPayoutAsync(string playerId, decimal usd) => throw new NotImplementedException();
}
