using Amp.Sdk.Crypto;
using System.Text.Json;

namespace Amp.Sdk;

/// <summary>
/// AMPClient — the main entry point for the AMP SDK.
///
/// Handles the full lifecycle: wallet login, queue, match, report, settle.
///
/// <example>
/// <code>
/// var amp = new AMPClient(
///     "https://amp.playwithamp.xyz",
///     new PrivateKeySigner(privateKey)
/// );
/// await amp.LoginAsync();
/// await amp.JoinQueueAsync("amp-tactics", "ranked-1v1");
/// amp.On&lt;MatchFound&gt;("match_found", match =>
/// {
///     Console.WriteLine($"Matched! {match.Opponent.Wallet}");
/// });
/// </code>
/// </example>
/// </summary>
public class AMPClient : IDisposable
{
    private readonly AmpRestClient _rest;
    private AmpWebSocket? _ws;
    private readonly IAMPSigner? _signer;
    private readonly IAMPCustodialProvider? _custodial;
    private readonly string? _playerId;
    private string? _wallet;

    public string? Wallet => _wallet;
    public bool Authenticated => _rest.Token != null;

    public AMPClient(
        string serverUrl,
        IAMPSigner? signer = null,
        IAMPCustodialProvider? custodial = null,
        string? playerId = null,
        TimeSpan? timeout = null)
    {
        _rest = new AmpRestClient(serverUrl, timeout);
        _signer = signer;
        _custodial = custodial;
        _playerId = playerId;
    }

    // ── Auth ────────────────────────────────────────────

    /// <summary>
    /// Log in with the configured signer. Gasless — one EIP-191 signature.
    /// </summary>
    public async Task<Player> LoginAsync()
    {
        if (_signer == null && _custodial == null)
            throw new InvalidOperationException("No signer or custodial provider configured");

        _wallet = _signer != null
            ? await _signer.GetAddress()
            : await _custodial!.GetAddress(_playerId!);

        var challengeResp = await _rest.PostAsync<JsonElement>("/v1/auth/challenge",
            new { wallet = _wallet });
        var challenge = challengeResp.GetProperty("challenge").GetString()
            ?? throw new AMPException("parse", "No challenge in response");

        var signature = _signer != null
            ? await _signer.SignPersonalSign(challenge)
            : await _custodial!.SignPersonalSign(_playerId!, challenge);

        var verifyResp = await _rest.PostAsync<JsonElement>("/v1/auth/verify",
            new { wallet = _wallet, signature, challenge });
        var token = verifyResp.GetProperty("token").GetString()
            ?? throw new AMPException("parse", "No token in response");

        _rest.SetToken(token);
        return new Player { Wallet = _wallet };
    }

    /// <summary>Log out and clear the session.</summary>
    public void Logout()
    {
        _rest.SetToken(null);
        _wallet = null;
        _ws?.Dispose();
        _ws = null;
    }

    // ── Player ──────────────────────────────────────────

    /// <summary>Get the authenticated player's info and ratings.</summary>
    public Task<PlayerMe> MeAsync() => _rest.GetAsync<PlayerMe>("/v1/me");

    /// <summary>Get any player's public profile.</summary>
    public Task<JsonElement> GetPlayerAsync(string wallet)
        => _rest.GetAsync<JsonElement>($"/v1/players/{wallet}");

    // ── Games ───────────────────────────────────────────

    /// <summary>List available games with live queue depth.</summary>
    public Task<GamesResponse> GamesAsync() => _rest.GetAsync<GamesResponse>("/v1/games");

    // ── Queue ───────────────────────────────────────────

    /// <summary>Join a ranked queue.</summary>
    public Task<QueueJoinResponse> JoinQueueAsync(string gameId, string rulesetId)
        => _rest.PostAsync<QueueJoinResponse>("/v1/queue/join",
            new { gameId, rulesetId });

    /// <summary>Leave the queue.</summary>
    public Task<JsonElement> LeaveQueueAsync()
        => _rest.PostAsync<JsonElement>("/v1/queue/leave");

    /// <summary>Get current queue status.</summary>
    public Task<QueueStatusResponse> QueueStatusAsync()
        => _rest.GetAsync<QueueStatusResponse>("/v1/queue/status");

    /// <summary>Skip the wait and play a bot immediately.</summary>
    public Task<PlayBotResponse> PlayBotAsync()
        => _rest.PostAsync<PlayBotResponse>("/v1/queue/play-bot");

    // ── Matches ─────────────────────────────────────────

    /// <summary>Get a match by ID.</summary>
    public Task<MatchView> GetMatchAsync(string matchId)
        => _rest.GetAsync<MatchView>($"/v1/matches/{matchId}");

    /// <summary>Get match history.</summary>
    public Task<JsonElement> MatchHistoryAsync(int limit = 20, int offset = 0)
        => _rest.GetAsync<JsonElement>($"/v1/matches/history?limit={limit}&offset={offset}");

    /// <summary>
    /// Report a 1v1 match result. Auto-signs with EIP-191 if a signer is available.
    /// </summary>
    public async Task<MatchReportResponse> ReportMatchAsync(
        string matchId, string result, string? transcriptHash = null)
    {
        string? signature = null;

        if (_signer != null)
        {
            var message = CryptoHelpers.BuildReportMessage(matchId, result);
            signature = await _signer.SignPersonalSign(message);
        }
        else if (_custodial != null && _playerId != null)
        {
            var message = CryptoHelpers.BuildReportMessage(matchId, result);
            signature = await _custodial.SignPersonalSign(_playerId, message);
        }

        return await _rest.PostAsync<MatchReportResponse>(
            $"/v1/matches/{matchId}/report",
            new { result, transcriptHash, signature });
    }

    // ── Parties ─────────────────────────────────────────

    /// <summary>Create a party (returns an invite code).</summary>
    public Task<CreatePartyResponse> CreatePartyAsync(string gameId, string rulesetId)
        => _rest.PostAsync<CreatePartyResponse>("/v1/parties",
            new { game_id = gameId, ruleset_id = rulesetId });

    /// <summary>Join a party by invite code.</summary>
    public Task<JsonElement> JoinPartyAsync(string inviteCode)
        => _rest.PostAsync<JsonElement>("/v1/parties/join",
            new { invite_code = inviteCode.ToUpperInvariant() });

    /// <summary>Get party details.</summary>
    public Task<PartyInfo> GetPartyAsync(string partyId)
        => _rest.GetAsync<PartyInfo>($"/v1/parties/{partyId}");

    /// <summary>Lock the party (leader only).</summary>
    public Task<JsonElement> LockPartyAsync(string partyId)
        => _rest.PostAsync<JsonElement>($"/v1/parties/{partyId}/lock");

    /// <summary>Disband the party (leader only).</summary>
    public Task<JsonElement> DisbandPartyAsync(string partyId)
        => _rest.PostAsync<JsonElement>($"/v1/parties/{partyId}/disband");

    // ── Events (WebSocket) ──────────────────────────────

    /// <summary>
    /// Subscribe to a WebSocket event. Connects automatically on first subscription.
    /// </summary>
    public void On<T>(string eventType, Action<T> handler) where T : class
    {
        if (_ws == null)
        {
            var token = _rest.Token
                ?? throw new InvalidOperationException("Must call LoginAsync() before subscribing to events");
            var baseUrl = ExtractBaseUrl();
            _ws = new AmpWebSocket(baseUrl, token);
            _ = _ws.ConnectAsync();
        }
        _ws.On(eventType, handler);
    }

    private string ExtractBaseUrl()
    {
        // Extract base URL from the REST client via reflection or re-store it
        // For simplicity, we store it in construction
        return _baseUrlCache ?? "https://amp.playwithamp.xyz";
    }

    private string? _baseUrlCache;

    public AMPClient WithBaseUrl(string baseUrl)
    {
        _baseUrlCache = baseUrl;
        return this;
    }

    public void Dispose()
    {
        _rest.Dispose();
        _ws?.Dispose();
    }
}
