using System.Numerics;
using Amp.Sdk;

/// <summary>
/// Example showing how to use the Avalanche Tournament Protocol (AMPTournamentCup)
/// via AmpTournamentClient to query cups, verify claims, attest results, and claim prizes.
/// </summary>
public static class TournamentExample
{
    public static async Task Main(string[] args)
    {
        // 1. Initialize client connected to Avalanche Fuji Testnet
        var client = new AmpTournamentClient();

        Console.WriteLine("🏆 Connecting to Avalanche Tournament Protocol on Fuji...");
        Console.WriteLine($"Contract: {client.ContractAddress}");
        Console.WriteLine($"RPC:      {client.RpcUrl}");

        // 2. Fetch on-chain tournament data (e.g. Tournament #8)
        var tournamentId = new BigInteger(8);
        var tournament = await client.GetTournamentAsync(tournamentId);

        Console.WriteLine($"\n📋 {tournament}");
        Console.WriteLine($"   Sponsor:         {tournament.Sponsor}");
        Console.WriteLine($"   Prize Pool:      {tournament.PrizePoolAvax:F2} AVAX ({tournament.PrizePoolWei} wei)");
        Console.WriteLine($"   Payout BPS:      {string.Join(", ", tournament.PayoutBps)}");
        Console.WriteLine($"   State:           {tournament.State}");
        Console.WriteLine($"   Winners Count:   {tournament.Winners.Length}");

        // 3. Check if a player won and can claim their share
        var playerAddress = tournament.Winners.Length > 0
            ? tournament.Winners[0]
            : "0x996204c3e14fc538aFA059b30368d0Fa8f12AD9a";

        var claimStatus = await client.CanClaimPrizeAsync(tournamentId, playerAddress);
        Console.WriteLine($"\n🔍 Claim check for {playerAddress}:");
        Console.WriteLine($"   {claimStatus}");

        if (claimStatus.CanClaim)
        {
            Console.WriteLine($"   Eligible to claim placement #{claimStatus.Placement + 1} prize: {claimStatus.EstimatedPrizeAvax:F4} AVAX!");

            // 4. If configured with a private key, submit the claimPrize transaction
            var winnerPrivateKey = Environment.GetEnvironmentVariable("WINNER_PRIVATE_KEY");
            if (!string.IsNullOrEmpty(winnerPrivateKey))
            {
                var winnerClient = AmpTournamentClient.FromPrivateKey(winnerPrivateKey);
                Console.WriteLine("   Submitting on-chain claimPrize transaction...");
                var txHash = await winnerClient.ClaimPrizeAsync(tournamentId, claimStatus.Placement);
                Console.WriteLine($"   Transaction broadcast! Hash: {txHash}");
            }
        }

        // 5. Verifier Attestation Example (EIP-712)
        var verifierKey = Environment.GetEnvironmentVariable("VERIFIER_PRIVATE_KEY");
        if (!string.IsNullOrEmpty(verifierKey))
        {
            var verifierClient = AmpTournamentClient.FromPrivateKey(verifierKey);
            var winners = new[]
            {
                "0x1563915e194D8CfBA1943570603F7606A3115508",
                "0x5CbDd86a2FA8Dc4bDdd8a8f69dBa48572EeC07FB"
            };

            var attestationSig = await verifierClient.SignTournamentResultAsync(tournamentId, winners);
            Console.WriteLine($"\n✍️  EIP-712 Attestation Signature: {attestationSig}");
        }
    }
}
