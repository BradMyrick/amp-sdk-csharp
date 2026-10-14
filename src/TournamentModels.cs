using System.Numerics;
using System.Text.Json.Serialization;

namespace Amp.Sdk;

/// <summary>
/// Status of an AMPTournamentCup prize pool.
/// </summary>
public enum TournamentState : byte
{
    Open = 0,       // Funded, awaiting verifier finalization
    Finalized = 1,  // Winners attested; prize claims open
    Complete = 2,   // All winners have claimed
    Cancelled = 3   // Sponsor refunded
}

/// <summary>
/// Representation of a timestamp supporting numeric (unix seconds) and DateTimeOffset operations.
/// </summary>
public readonly struct TournamentTimestamp : IEquatable<TournamentTimestamp>, IComparable<TournamentTimestamp>
{
    private readonly ulong _seconds;

    public TournamentTimestamp(ulong seconds) => _seconds = seconds;
    public TournamentTimestamp(long seconds) => _seconds = seconds > 0 ? (ulong)seconds : 0;
    public TournamentTimestamp(DateTimeOffset dto) => _seconds = (ulong)Math.Max(0, dto.ToUnixTimeSeconds());

    public ulong Seconds => _seconds;
    public DateTimeOffset DateTimeUtc => DateTimeOffset.FromUnixTimeSeconds((long)_seconds);
    public DateTime DateTime => DateTimeUtc.UtcDateTime;

    public long ToUnixTimeSeconds() => (long)_seconds;

    public static implicit operator ulong(TournamentTimestamp ts) => ts._seconds;
    public static implicit operator long(TournamentTimestamp ts) => (long)ts._seconds;
    public static implicit operator DateTimeOffset(TournamentTimestamp ts) => ts.DateTimeUtc;
    public static implicit operator TournamentTimestamp(ulong seconds) => new(seconds);
    public static implicit operator TournamentTimestamp(long seconds) => new(seconds);
    public static implicit operator TournamentTimestamp(DateTimeOffset dto) => new(dto);

    public static bool operator >(TournamentTimestamp a, TournamentTimestamp b) => a._seconds > b._seconds;
    public static bool operator <(TournamentTimestamp a, TournamentTimestamp b) => a._seconds < b._seconds;
    public static bool operator >=(TournamentTimestamp a, TournamentTimestamp b) => a._seconds >= b._seconds;
    public static bool operator <=(TournamentTimestamp a, TournamentTimestamp b) => a._seconds <= b._seconds;

    public static bool operator >(TournamentTimestamp a, int b) => (long)a._seconds > b;
    public static bool operator <(TournamentTimestamp a, int b) => (long)a._seconds < b;
    public static bool operator >=(TournamentTimestamp a, int b) => (long)a._seconds >= b;
    public static bool operator <=(TournamentTimestamp a, int b) => (long)a._seconds <= b;

    public static bool operator >(TournamentTimestamp a, ulong b) => a._seconds > b;
    public static bool operator <(TournamentTimestamp a, ulong b) => a._seconds < b;
    public static bool operator >=(TournamentTimestamp a, ulong b) => a._seconds >= b;
    public static bool operator <=(TournamentTimestamp a, ulong b) => a._seconds <= b;

    public static bool operator >(TournamentTimestamp a, long b) => (long)a._seconds > b;
    public static bool operator <(TournamentTimestamp a, long b) => (long)a._seconds < b;
    public static bool operator >=(TournamentTimestamp a, long b) => (long)a._seconds >= b;
    public static bool operator <=(TournamentTimestamp a, long b) => (long)a._seconds <= b;

    public static bool operator >(TournamentTimestamp a, DateTimeOffset b) => a.DateTimeUtc > b;
    public static bool operator <(TournamentTimestamp a, DateTimeOffset b) => a.DateTimeUtc < b;
    public static bool operator >=(TournamentTimestamp a, DateTimeOffset b) => a.DateTimeUtc >= b;
    public static bool operator <=(TournamentTimestamp a, DateTimeOffset b) => a.DateTimeUtc <= b;

    public static bool operator ==(TournamentTimestamp a, TournamentTimestamp b) => a._seconds == b._seconds;
    public static bool operator !=(TournamentTimestamp a, TournamentTimestamp b) => a._seconds != b._seconds;

    public bool Equals(TournamentTimestamp other) => _seconds == other._seconds;
    public override bool Equals(object? obj) => obj is TournamentTimestamp other && Equals(other);
    public override int GetHashCode() => _seconds.GetHashCode();
    public int CompareTo(TournamentTimestamp other) => _seconds.CompareTo(other._seconds);

    public override string ToString() => DateTimeUtc.ToString("u");
}

/// <summary>
/// On-chain tournament data stored in AMPTournamentCup.
/// </summary>
public class TournamentData
{
    [JsonPropertyName("id")]
    public BigInteger Id { get; set; }

    [JsonPropertyName("sponsor")]
    public string Sponsor { get; set; } = "";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "0x0000000000000000000000000000000000000000";

    [JsonPropertyName("prizePoolWei")]
    public BigInteger PrizePoolWei { get; set; } = BigInteger.Zero;

    /// <summary>Alias for PrizePoolWei.</summary>
    [JsonIgnore]
    public BigInteger PrizePool
    {
        get => PrizePoolWei;
        set => PrizePoolWei = value;
    }

    /// <summary>Prize pool expressed in AVAX (1e18 wei = 1 AVAX).</summary>
    [JsonPropertyName("prizePoolAvax")]
    public decimal PrizePoolAvax => (decimal)PrizePoolWei / 1_000_000_000_000_000_000m;

    /// <summary>Prize pool expressed in double for floating-point calculations.</summary>
    [JsonIgnore]
    public double PrizePoolAvaxDouble => (double)PrizePoolWei / 1e18;

    /// <summary>Basis points per placement (10000 bps = 100%). Index 0 = 1st place.</summary>
    [JsonPropertyName("payoutBps")]
    public int[] PayoutBps { get; set; } = Array.Empty<int>();

    /// <summary>Sole authorized attester for this cup.</summary>
    [JsonPropertyName("verifier")]
    public string Verifier { get; set; } = "";

    /// <summary>Attested winners, index-aligned to payoutBps.</summary>
    [JsonPropertyName("winners")]
    public string[] Winners { get; set; } = Array.Empty<string>();

    [JsonPropertyName("state")]
    public TournamentState State { get; set; } = TournamentState.Open;

    [JsonPropertyName("createdAt")]
    public TournamentTimestamp CreatedAt { get; set; }

    [JsonPropertyName("finalizeDeadline")]
    public TournamentTimestamp FinalizeDeadline { get; set; }

    [JsonIgnore]
    public ulong CreatedAtTimestamp => CreatedAt.Seconds;

    [JsonIgnore]
    public ulong FinalizeDeadlineTimestamp => FinalizeDeadline.Seconds;

    /// <summary>True if the tournament prize pool is in native AVAX (address 0x0).</summary>
    [JsonIgnore]
    public bool IsNativeAvax =>
        string.IsNullOrEmpty(Token) ||
        Token.Equals("0x0000000000000000000000000000000000000000", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsOpen => State == TournamentState.Open;

    [JsonIgnore]
    public bool IsFinalized => State == TournamentState.Finalized;

    [JsonIgnore]
    public bool IsComplete => State == TournamentState.Complete;

    [JsonIgnore]
    public bool IsCancelled => State == TournamentState.Cancelled;

    /// <summary>Calculate the share amount in wei for a specific 0-indexed placement.</summary>
    public BigInteger GetPlacementPrizeWei(int placement)
    {
        if (PayoutBps == null || placement < 0 || placement >= PayoutBps.Length)
            return BigInteger.Zero;
        return (PrizePoolWei * PayoutBps[placement]) / 10000;
    }

    /// <summary>Calculate the share amount in AVAX for a specific 0-indexed placement.</summary>
    public decimal GetPlacementPrizeAvax(int placement) =>
        (decimal)GetPlacementPrizeWei(placement) / 1_000_000_000_000_000_000m;

    public override string ToString() =>
        $"Tournament #{Id} [{State}]: {PrizePoolAvax:F4} AVAX pool, {Winners?.Length ?? 0} winners attested";
}

/// <summary>
/// Claim verification result for a player in a tournament.
/// </summary>
public class CanClaimResult
{
    public bool CanClaim { get; set; }
    public bool IsWinner { get; set; }

    /// <summary>0-indexed placement (0 = 1st place). -1 if not a winner.</summary>
    public int Placement { get; set; } = -1;

    public BigInteger EstimatedPrizeWei { get; set; } = BigInteger.Zero;
    public decimal EstimatedPrizeAvax { get; set; } = 0m;
    public bool IsClaimed { get; set; }

    /// <summary>Alias for IsClaimed.</summary>
    public bool AlreadyClaimed => IsClaimed;

    public string Reason { get; set; } = "";
    public TournamentData? Tournament { get; set; }

    public override string ToString()
    {
        if (CanClaim)
            return $"Winner! Placement #{Placement + 1}, Claimable Prize: {EstimatedPrizeAvax:F4} AVAX";
        if (IsClaimed)
            return $"Placement #{Placement + 1} prize already claimed";
        if (IsWinner)
            return $"Winner (Placement #{Placement + 1}), but prize cannot be claimed: {Reason}";
        return $"Not a winner: {Reason}";
    }
}
