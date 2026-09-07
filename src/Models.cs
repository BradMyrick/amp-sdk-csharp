using System.Text.Json.Serialization;

namespace Amp.Sdk;

public class Player
{
    [JsonPropertyName("wallet")] public string Wallet { get; set; } = "";
    [JsonPropertyName("region")] public string Region { get; set; } = "na";
    [JsonPropertyName("language")] public string Language { get; set; } = "en";
}

public class PlayerRating
{
    [JsonPropertyName("gameId")] public string GameId { get; set; } = "";
    [JsonPropertyName("rulesetId")] public string RulesetId { get; set; } = "";
    [JsonPropertyName("rating")] public double Rating { get; set; }
    [JsonPropertyName("deviation")] public double Deviation { get; set; }
    [JsonPropertyName("wins")] public int Wins { get; set; }
    [JsonPropertyName("losses")] public int Losses { get; set; }
    [JsonPropertyName("draws")] public int Draws { get; set; }
}

public class Ruleset
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("queueDepth")] public int QueueDepth { get; set; }
}

public class GameInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("rulesets")] public List<Ruleset> Rulesets { get; set; } = new();
    [JsonPropertyName("nextQueueWindowUtc")] public string? NextQueueWindowUtc { get; set; }
}

public class GamesResponse
{
    [JsonPropertyName("games")] public List<GameInfo> Games { get; set; } = new();
    [JsonPropertyName("stakingEnabled")] public bool StakingEnabled { get; set; }
    [JsonPropertyName("chainId")] public long ChainId { get; set; }
}

public class QueueJoinResponse
{
    [JsonPropertyName("ticketId")] public string TicketId { get; set; } = "";
    [JsonPropertyName("queueDepth")] public int QueueDepth { get; set; }
    [JsonPropertyName("skillWindow")] public double SkillWindow { get; set; }
    [JsonPropertyName("rating")] public double? Rating { get; set; }
}

public class QueueStatusResponse
{
    [JsonPropertyName("queued")] public bool Queued { get; set; }
    [JsonPropertyName("depth")] public int? Depth { get; set; }
    [JsonPropertyName("waitedMs")] public long? WaitedMs { get; set; }
    [JsonPropertyName("skillWindow")] public double? SkillWindow { get; set; }
}

public class MatchFound
{
    [JsonPropertyName("matchId")] public string MatchId { get; set; } = "";
    [JsonPropertyName("gameId")] public string GameId { get; set; } = "";
    [JsonPropertyName("bot")] public bool Bot { get; set; }
    [JsonPropertyName("opponent")] public OpponentInfo Opponent { get; set; } = new();
    [JsonPropertyName("yourRating")] public double YourRating { get; set; }
    [JsonPropertyName("expiresAt")] public string ExpiresAt { get; set; } = "";
}

public class OpponentInfo
{
    [JsonPropertyName("wallet")] public string Wallet { get; set; } = "";
    [JsonPropertyName("rating")] public double Rating { get; set; }
    [JsonPropertyName("region")] public string Region { get; set; } = "";
}

public class MatchView
{
    [JsonPropertyName("matchId")] public string MatchId { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("bot")] public bool Bot { get; set; }
    [JsonPropertyName("outcome")] public string? Outcome { get; set; }
    [JsonPropertyName("winner")] public string? Winner { get; set; }
    [JsonPropertyName("expiresAt")] public string ExpiresAt { get; set; } = "";
}

public class MatchReportResponse
{
    [JsonPropertyName("matchId")] public string MatchId { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "";
}

public class PartyInfo
{
    [JsonPropertyName("partyId")] public string PartyId { get; set; } = "";
    [JsonPropertyName("leader")] public string Leader { get; set; } = "";
    [JsonPropertyName("inviteCode")] public string InviteCode { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("gameId")] public string GameId { get; set; } = "";
    [JsonPropertyName("rulesetId")] public string RulesetId { get; set; } = "";
}

public class CreatePartyResponse
{
    [JsonPropertyName("partyId")] public string PartyId { get; set; } = "";
    [JsonPropertyName("inviteCode")] public string InviteCode { get; set; } = "";
    [JsonPropertyName("leader")] public string Leader { get; set; } = "";
}

public class PlayerMe
{
    [JsonPropertyName("wallet")] public string Wallet { get; set; } = "";
    [JsonPropertyName("ratings")] public List<PlayerRating> Ratings { get; set; } = new();
    [JsonPropertyName("liveMatchId")] public string? LiveMatchId { get; set; }
}

public class PlayBotResponse
{
    [JsonPropertyName("matchId")] public string MatchId { get; set; } = "";
    [JsonPropertyName("bot")] public bool Bot { get; set; }
}

public class AMPException : Exception
{
    public string Code { get; }
    public int Status { get; }

    public AMPException(string code, string message, int status = 0)
        : base(message)
    {
        Code = code;
        Status = status;
    }
}
