using System.Text.Json;

namespace ProFlowApp.Services;

public class ClassifierClient
{
    private readonly HttpClient _http;

    public ClassifierClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> ClassifyAsync(string kategori, string keterangan)
    {
        var response = await _http.PostAsJsonAsync("http://localhost:8000/classify", new
        {
            category = kategori,
            keterangan = keterangan
        });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("urgency_level").GetString() ?? "Medium";
    }
}