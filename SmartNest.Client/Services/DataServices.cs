using System.Net.Http.Json;
using SmartNest.Shared.DTOs;

namespace SmartNest.Client.Services;

public class SensorService
{
    private readonly HttpClient _http;
    public SensorService(HttpClient http) => _http = http;

    public async Task<SensorDataDto?> GetLatestAsync()
    { try { return await _http.GetFromJsonAsync<SensorDataDto>("api/sensor/latest"); } catch { return null; } }

    public async Task<List<SensorDataDto>> GetHistoryAsync(int hours = 24)
    { try { return await _http.GetFromJsonAsync<List<SensorDataDto>>($"api/sensor/history?hours={hours}") ?? new(); } catch { return new(); } }

    public async Task<SensorStatsDto?> GetStatsAsync(int hours = 24)
    { try { return await _http.GetFromJsonAsync<SensorStatsDto>($"api/sensor/stats?hours={hours}"); } catch { return null; } }

    public async Task<(bool ok, string msg)> PostAsync(SensorDataDto dto)
    { try { var r = await _http.PostAsJsonAsync("api/sensor", dto); return r.IsSuccessStatusCode ? (true, "Données transmises.") : (false, "Erreur serveur."); } catch (Exception e) { return (false, e.Message); } }
}

public class ChickService
{
    private readonly HttpClient _http;
    public ChickService(HttpClient http) => _http = http;

    public async Task<ChickDto?> GetLatestAsync()
    { try { return await _http.GetFromJsonAsync<ChickDto>("api/chick/latest"); } catch { return null; } }

    public async Task<(bool ok, string msg)> PostAsync(ChickDto dto)
    { try { var r = await _http.PostAsJsonAsync("api/chick", dto); return r.IsSuccessStatusCode ? (true, "Comptage enregistré.") : (false, "Erreur serveur."); } catch (Exception e) { return (false, e.Message); } }
}

public class NotificationService
{
    private readonly HttpClient _http;
    public NotificationService(HttpClient http) => _http = http;

    public async Task<NotificationSettingDto?> GetSettingsAsync()
    { try { return await _http.GetFromJsonAsync<NotificationSettingDto>("api/notification/settings"); } catch { return null; } }

    public async Task<(bool ok, string msg)> UpdateSettingsAsync(NotificationSettingDto dto)
    { try { var r = await _http.PutAsJsonAsync("api/notification/settings", dto); return r.IsSuccessStatusCode ? (true, "Paramètres sauvegardés.") : (false, "Erreur serveur."); } catch (Exception e) { return (false, e.Message); } }

    public async Task<List<AlertDto>> GetAlertsAsync(bool unreadOnly = false)
    { try { return await _http.GetFromJsonAsync<List<AlertDto>>($"api/notification/alerts?unreadOnly={unreadOnly}") ?? new(); } catch { return new(); } }

    public async Task MarkAllReadAsync()
    { try { await _http.PutAsync("api/notification/alerts/read-all", null); } catch { } }

    public async Task MarkReadAsync(int id)
    { try { await _http.PutAsync($"api/notification/alerts/{id}/read", null); } catch { } }
}

public class VideoService
{
    private readonly HttpClient _http;
    public VideoService(HttpClient http) => _http = http;

    public async Task<List<VideoRecordingDto>> GetListAsync()
    { try { return await _http.GetFromJsonAsync<List<VideoRecordingDto>>("api/video/list") ?? new(); } catch { return new(); } }

    public async Task DeleteAsync(string fileName)
    { try { await _http.DeleteAsync($"api/video/{fileName}"); } catch { } }
}
