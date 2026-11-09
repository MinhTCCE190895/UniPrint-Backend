using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniPrint.Web.Services;

/// <summary>
/// Wrapper HttpClient giao tiếp với UniPrint.API, tự động đính kèm JWT từ Session.
/// </summary>
public class WebApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _config;

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public WebApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor, IConfiguration config)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _config = config;
        var baseUrl = config["ApiSettings:BaseUrl"] ?? "http://localhost:5045";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    // ─────────────────────────── HELPERS ───────────────────────────

    private void AttachToken()
    {
        var token = _httpContextAccessor.HttpContext?.Session.GetString("jwt_token");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json)) return default;
        try 
        { 
            return JsonSerializer.Deserialize<T>(json, JsonOpts); 
        }
        catch 
        { 
            return default; 
        }
    }

    // ─────────────────────────── GET ───────────────────────────

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        AttachToken();
        var response = await _http.GetAsync(endpoint);
        return await DeserializeAsync<T>(response);
    }

    // ─────────────────────────── POST ───────────────────────────

    public async Task<T?> PostAsync<T>(string endpoint, object? body = null)
    {
        AttachToken();
        HttpContent? content = body == null ? null
            : new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(endpoint, content);
        return await DeserializeAsync<T>(response);
    }

    public async Task<T?> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent form)
    {
        AttachToken();
        var response = await _http.PostAsync(endpoint, form);
        return await DeserializeAsync<T>(response);
    }

    // ─────────────────────────── PUT ───────────────────────────

    public async Task<T?> PutAsync<T>(string endpoint, object? body = null)
    {
        AttachToken();
        HttpContent? content = body == null ? null
            : new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
        var response = await _http.PutAsync(endpoint, content);
        return await DeserializeAsync<T>(response);
    }

    // ─────────────────────────── PATCH ───────────────────────────

    public async Task<T?> PatchAsync<T>(string endpoint, object? body = null)
    {
        AttachToken();
        HttpContent? content = body == null ? null
            : new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
        var response = await _http.SendAsync(request);
        return await DeserializeAsync<T>(response);
    }

    // ─────────────────────────── DELETE ───────────────────────────

    public async Task<T?> DeleteAsync<T>(string endpoint)
    {
        AttachToken();
        var response = await _http.DeleteAsync(endpoint);
        return await DeserializeAsync<T>(response);
    }
}
