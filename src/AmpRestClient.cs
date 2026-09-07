using System.Net.Http.Json;
using System.Text.Json;

namespace Amp.Sdk;

/// <summary>
/// Low-level REST client for the AMP matchmaker API.
/// </summary>
public class AmpRestClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private string? _token;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AmpRestClient(string baseUrl, TimeSpan? timeout = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(10) };
    }

    public void SetToken(string? token) => _token = token;
    public string? Token => _token;

    public async Task<T> GetAsync<T>(string path)
    {
        return await SendAsync<T>(HttpMethod.Get, path, null);
    }

    public async Task<T> PostAsync<T>(string path, object? body = null)
    {
        return await SendAsync<T>(HttpMethod.Post, path, body);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");

        if (_token != null)
            request.Headers.Authorization = new("Bearer", _token);

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var err = JsonSerializer.Deserialize<JsonElement>(json);
                var code = err.TryGetProperty("error", out var c) ? c.GetString() : "unknown";
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : response.ReasonPhrase;
                throw new AMPException(code ?? "unknown", msg ?? "unknown", (int)response.StatusCode);
            }
            catch (JsonException)
            {
                throw new AMPException("unknown", response.ReasonPhrase ?? "unknown", (int)response.StatusCode);
            }
        }

        return JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new AMPException("parse", $"Failed to parse response from {path}");
    }

    public void Dispose() => _http.Dispose();
}
