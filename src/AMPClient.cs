using Amp.Sdk.Crypto;
using System.Text.Json;

namespace Amp.Sdk;

/// <summary>
/// AMPClient — the main entry point for the AMP SDK.
///
/// Handles the full lifecycle: wallet login, queue, match, report, settle,
/// party, multiplayer, and real-time events.
/// </summary>
public class AMPClient : IDisposable
{
    private readonly AmpRestClient _rest;
    private readonly string _baseUrl;
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
        _baseUrl = serverUrl.TrimEnd('/');
        _rest = new AmpRestClient(_baseUrl, timeout);
        _signer = signer;
        _custodial = custodial;
        _playerId = playerId;
    }

    // ── Auth ────────────────────────────────────────────

    /// <summary>Log in with the configured signer. Gasless — one EIP-191 signature.</summary>
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
        => _rest.PostAsync<QueueJoinResponse>("/v1/queue/join", new { gameId, rulesetId });

    /// <summary>Leave the queue.</summary>
    public Task<JsonElement> LeaveQueueAsync()
        => _rest.PostAsync<JsonElement>("/v1/queue/leave");

    /// <summary>Get current queue status.</summary>
    public Task<QueueStatusResponse> QueueStatusAsync()
        => _rest.GetAsync<QueueStatusResponse>("/v1/queue/status");

    /// <summary>Skip the wait and play a bot immediately.</summary>
    public Task<PlayBotResponse> PlayBotAsync()
        => _rest.PostAsync<PlayBotResponse>("/v1/queue/play-bot");

    // ── Matches (1v1) ───────────────────────────────────

    /// <summary>Get a match by ID.</summary>
    public Task<MatchView> GetMatchAsync(string matchId)
        => _rest.GetAsync<MatchView>($"/v1/matches/{matchId}");

    /// <summary>Get match history.</summary>
    public Task<JsonElement> MatchHistoryAsync(int limit = 20, int offset = 0)
        => _rest.GetAsync<JsonElement>($"/v1/matches/history?limit={limit}&offset={offset}");

    /// <summary>Report a 1v1 match result. Auto-signs with EIP-191.</summary>
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

    // ── Multiplayer (N-player) ──────────────────────────

    /// <summary>
    /// Commit to a staked FFA queue. Generates a salt internally and
    /// returns it for the reveal phase.
    /// </summary>
    public async Task<MultiCommitResponse> MultiCommitAsync(
        string gameId, long stakeWei, int lobbySize)
    {
        var salt = CryptoHelpers.GenerateSalt();
        var wallet = _wallet ?? throw new InvalidOperationException("Must call LoginAsync() first");

        // Compute the commit hash: keccak256(addr ‖ stake ‖ salt)
        var commitHash = await CryptoHelpers.ComputeCommitHashAsync(wallet, stakeWei, salt);

        var result = await _rest.PostAsync<JsonElement>("/v1/multi/commit",
            new { gameId, commitHash, stakeWei, lobbySize });

        return new MultiCommitResponse
        {
            Committed = result.GetProperty("committed").GetBoolean(),
            CommittedCount = result.GetProperty("committedCount").GetInt32(),
            Ready = result.GetProperty("ready").GetBoolean(),
            Salt = salt,
        };
    }

    /// <summary>Reveal your commit salt.</summary>
    public Task<JsonElement> MultiRevealAsync(string gameId, string rulesetId, string salt)
        => _rest.PostAsync<JsonElement>("/v1/multi/reveal",
            new { gameId, rulesetId, salt });

    /// <summary>Get a multiplayer match.</summary>
    public Task<JsonElement> GetMultiMatchAsync(string matchId)
        => _rest.GetAsync<JsonElement>($"/v1/multi/{matchId}");

    /// <summary>
    /// Report a multiplayer ladder. Auto-signs EIP-712 typed data.
    /// </summary>
    public async Task<JsonElement> MultiReportAsync(
        string matchId,
        (string wallet, int rank)[] ranked,
        string transcriptHash,
        long sessionNonce,
        long? chainId = null,
        string? contractAddress = null)
    {
        string? signature = null;
        var rankedArray = ranked.Select(r => new object[] { r.wallet, r.rank }).ToArray();

        if (_signer != null || (_custodial != null && _playerId != null))
        {
            var typedData = CryptoHelpers.BuildLadderTypedData(
                chainId ?? 43113,
                contractAddress ?? "0xcabf7b626172fE55d54f03c346563671AbcC77f7",
                matchId,
                gameId: "0x" + new string('0', 63) + "1",
                rankedPlacements: ranked.Select(r => r.wallet).ToArray(),
                transcriptHash,
                sessionNonce);

            signature = _signer != null
                ? await _signer.SignTypedData(typedData)
                : await _custodial!.SignTypedData(_playerId!, typedData);
        }

        return await _rest.PostAsync<JsonElement>($"/v1/multi/{matchId}/report",
            new { ranked = rankedArray, transcriptHash, sessionNonce, signature });
    }

    /// <summary>Trigger settlement for a quorum-reached multiplayer match.</summary>
    public Task<JsonElement> MultiClaimAsync(string matchId)
        => _rest.PostAsync<JsonElement>($"/v1/multi/{matchId}/claim");

    // ── Exit certificates (multiplayer death certs) ────

    /// <summary>
    /// Submit an exit certificate: an eliminated player signs their rank,
    /// exit frame, and state hash, then disconnects. Auto-signs EIP-191.
    /// </summary>
    public async Task<JsonElement> SubmitExitCertAsync(
        string matchId, int rank, long exitFrame, string stateHash)
    {
        var message = CryptoHelpers.BuildExitCertMessage(matchId, rank, exitFrame, stateHash);
        string? signature = null;
        if (_signer != null)
            signature = await _signer.SignPersonalSign(message);
        else if (_custodial != null && _playerId != null)
            signature = await _custodial.SignPersonalSign(_playerId, message);

        var body = new { rank, exitFrame, stateHash, signature };
        return await _rest.PostAsync<JsonElement>($"/v1/multi/{matchId}/exit", body);
    }

    /// <summary>
    /// Countersign another player's exit certificate as a survivor,
    /// verifying their state hash against your own simulation.
    /// </summary>
    public Task<JsonElement> CountersignExitCertAsync(
        string matchId, string wallet, string stateHash)
        => _rest.PostAsync<JsonElement>($"/v1/multi/{matchId}/exit/{wallet}",
            new { stateHash });

    // ── Staked 1v1 escrow ──────────────────────────────

    /// <summary>
    /// Verify on-chain escrow for a staked 1v1 match (participant only).
    /// Flips an escrow_pending match to live once both deposits check out.
    /// </summary>
    public Task<JsonElement> VerifyEscrowAsync(string matchId)
        => _rest.PostAsync<JsonElement>($"/v1/matches/{matchId}/escrow/verify", new { });

    // ── Convenience ────────────────────────────────────

    /// <summary>
    /// Wait for a match assignment after joining a queue. Listens on the
    /// WebSocket (with a REST polling fallback) and completes as soon as
    /// the match is found. Throws TimeoutException on expiry.
    /// </summary>
    public async Task<MatchFound> WaitForMatchAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var tcs = new TaskCompletionSource<MatchFound>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource(timeout.Value);

        var sub = On<MatchFound>("match_found", m => tcs.TrySetResult(m));

        // REST fallback poller (2 s) via me().liveMatchId
        var poller = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested && !tcs.Task.IsCompleted)
            {
                try
                {
                    var me = await MeAsync();
                    if (me.LiveMatchId != null)
                    {
                        var m = await GetMatchAsync(me.LiveMatchId);
                        tcs.TrySetResult(new MatchFound
                        {
                            MatchId = m.MatchId,
                            Bot = m.Bot,
                            ExpiresAt = m.ExpiresAt,
                        });
                        break;
                    }
                }
                catch { /* keep polling */ }
                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
            }
        });

        var timeoutTask = Task.Delay(timeout.Value, cts.Token);
        var done = await Task.WhenAny(tcs.Task, timeoutTask);
        cts.Cancel();
        sub.Dispose();
        try { await poller; } catch { /* cancelled */ }

        if (done != tcs.Task)
            throw new TimeoutException($"WaitForMatchAsync timed out after {timeout.Value.TotalSeconds:F0}s");

        return tcs.Task.Result;
    }

    // ── Events (WebSocket) ──────────────────────────────

    /// <summary>
    /// Subscribe to a WebSocket event. Connects automatically on first subscription.
    /// Returns a disposable that unsubscribes when disposed.
    /// </summary>
    public IDisposable On<T>(string eventType, Action<T> handler) where T : class
    {
        if (_ws == null)
        {
            var token = _rest.Token
                ?? throw new InvalidOperationException("Must call LoginAsync() before subscribing to events");
            _ws = new AmpWebSocket(_baseUrl, token);
            _ = _ws.ConnectAsync();
        }
        return _ws.On(eventType, handler);
    }

    /// <summary>Subscribe with the raw JSON payload (struct-safe).</summary>
    public IDisposable OnJson(string eventType, Action<JsonElement> handler)
    {
        if (_ws == null)
        {
            var token = _rest.Token
                ?? throw new InvalidOperationException("Must call LoginAsync() before subscribing to events");
            _ws = new AmpWebSocket(_baseUrl, token);
            _ = _ws.ConnectAsync();
        }
        return _ws.OnJson(eventType, handler);
    }

    /// <summary>Disconnect the WebSocket.</summary>
    public void Disconnect()
    {
        _ws?.Dispose();
        _ws = null;
    }

    public void Dispose()
    {
        _rest.Dispose();
        _ws?.Dispose();
    }
}

/// <summary>Response from MultiCommitAsync — includes the generated salt for reveal.</summary>
public class MultiCommitResponse
{
    public bool Committed { get; set; }
    public int CommittedCount { get; set; }
    public bool Ready { get; set; }
    public string Salt { get; set; } = "";
}
