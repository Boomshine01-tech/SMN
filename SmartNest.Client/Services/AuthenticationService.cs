using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;
using SmartNest.Shared.DTOs;

namespace SmartNest.Client.Services;

public class AuthenticationService
{
    private ILocalStorageService? _storage;
    private HttpClient?           _http;

    public event Action? OnAuthStateChanged;

    public bool   IsAuthenticated { get; private set; }
    public string Username        { get; private set; } = string.Empty;
    public string Email           { get; private set; } = string.Empty;
    public string Role            { get; private set; } = string.Empty;
    public string Token           { get; private set; } = string.Empty;
    public bool IsInitialized { get; private set; } = false;
    public event Action? OnInitialized;

    public async Task InitializeAsync(ILocalStorageService storage, HttpClient http)
    {
        _storage = storage;
        _http    = http;
        var token = await _storage.GetItemAsStringAsync("authToken");
        if (!string.IsNullOrWhiteSpace(token) && !IsExpired(token))
        {
            Token = token;
            ParseClaims(token);
            SetHeader();
            IsAuthenticated = true;
            var storedUsername = await _storage.GetItemAsStringAsync("authUsername");
            if (!string.IsNullOrEmpty(storedUsername))
                Username = storedUsername;
        }
         IsInitialized = true;         
         OnInitialized?.Invoke();
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        var resp   = await _http!.PostAsJsonAsync("api/auth/login", req);
        var result = await resp.Content.ReadFromJsonAsync<LoginResponse>() ?? new();
        if (result.Success) await Store(result);
        return result;
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest req)
    {
        var resp   = await _http!.PostAsJsonAsync("api/auth/register", req);
        var result = await resp.Content.ReadFromJsonAsync<LoginResponse>() ?? new();
        if (result.Success) await Store(result);
        return result;
    }

    public async Task LogoutAsync()
    {
        if (_storage != null)
            foreach (var key in new[] { "authToken", "tokenExpiration" })
                await _storage.RemoveItemAsync(key);
        Token = Username = Email = Role = string.Empty;
        IsAuthenticated = false;
        _http!.DefaultRequestHeaders.Authorization = null;
        OnAuthStateChanged?.Invoke();
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_storage == null) return false;
        var token = await _storage.GetItemAsStringAsync("authToken");
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (IsExpired(token)) { await LogoutAsync(); return false; }
        return true;
    }

    private async Task Store(LoginResponse r)
    {
        Token = r.Token; Username = r.Username; Email = r.Email; Role = r.Role;
        IsAuthenticated = true;
        await _storage!.SetItemAsStringAsync("authToken",         r.Token);
        await _storage.SetItemAsStringAsync ("authUsername", r.Username);
        await _storage.SetItemAsStringAsync ("tokenExpiration",   r.ExpiresAt.ToString("O"));
        SetHeader();
        OnAuthStateChanged?.Invoke();
    }

    private void SetHeader() =>
        _http!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

    private void ParseClaims(string token)
    {
        try
        {
            var parts   = token.Split('.');
            if (parts.Length != 3) return;
            var payload = parts[1];
            var padded  = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json    = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var dict    = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (dict == null) return;
            Username = Get(dict, "name", "unique_name", "username") ?? string.Empty;
            Email    = Get(dict, "email") ?? string.Empty;
            Role     = Get(dict, "role", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? string.Empty;
        }
        catch { }
    }

    private static string? Get(Dictionary<string, JsonElement> d, params string[] keys)
    {
        foreach (var k in keys) if (d.TryGetValue(k, out var v)) return v.GetString();
        return null;
    }

    private static bool IsExpired(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return true;
            var p = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
            var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        Encoding.UTF8.GetString(Convert.FromBase64String(p)));
            return !d!.TryGetValue("exp", out var exp) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp.GetInt64();
        }
        catch { return true; }
    }
}
