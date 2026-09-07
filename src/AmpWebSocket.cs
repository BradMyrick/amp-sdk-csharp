using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Amp.Sdk;

/// <summary>
/// WebSocket client with exponential-backoff auto-reconnect.
/// Event-driven — subscribe with On<T>(eventName, handler).
/// </summary>
public class AmpWebSocket : IDisposable
{
    private ClientWebSocket? _ws;
    private readonly string _url;
    private bool _closed;
    private int _attempt;
    private readonly CancellationTokenSource _cts = new();

    private readonly Dictionary<string, List<Func<JsonElement, Task>>> _handlers = new();

    public AmpWebSocket(string baseUrl, string token)
    {
        var wsUrl = baseUrl.Replace("http", "ws").Replace("https", "wss");
        _url = $"{wsUrl}/v1/ws?token={Uri.EscapeDataString(token)}";
    }

    public void On<T>(string eventType, Action<T> handler) where T : class
    {
        if (!_handlers.ContainsKey(eventType))
            _handlers[eventType] = new();

        _handlers[eventType].Add(async (data) =>
        {
            var typed = data.Deserialize<T>();
            if (typed != null)
                handler(typed);
            await Task.CompletedTask;
        });
    }

    public async Task ConnectAsync()
    {
        if (_closed) return;

        _ws = new ClientWebSocket();
        try
        {
            await _ws.ConnectAsync(new Uri(_url), _cts.Token);
            _attempt = 0;
            _ = ReceiveLoop();
        }
        catch
        {
            _ = ReconnectAsync();
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        try
        {
            while (_ws?.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ReconnectAsync();
                    return;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var json = sb.ToString();
                    sb.Clear();
                    ProcessMessage(json);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            await ReconnectAsync();
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<JsonElement>(json);
            var eventType = msg.GetProperty("type").GetString();
            var data = msg.TryGetProperty("data", out var d) ? d : default;

            if (eventType != null && _handlers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    _ = handler(data);
                }
            }
        }
        catch { /* malformed message */ }
    }

    private async Task ReconnectAsync()
    {
        if (_closed) return;

        _attempt++;
        var delay = Math.Min(1000 * Math.Pow(2, _attempt), 15000);
        await Task.Delay(TimeSpan.FromMilliseconds(delay));
        await ConnectAsync();
    }

    public void Dispose()
    {
        _closed = true;
        _cts.Cancel();
        _ws?.Dispose();
        _handlers.Clear();
    }
}
