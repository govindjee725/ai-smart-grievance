using System.Net.Http.Json;

namespace GrievanceService.Services;

public record AiPrediction(
    string Category,
    double Confidence,
    string Priority,
    string Model,
    bool ReviewRequired
);

public class AiClient(HttpClient httpClient)
{
    public async Task<AiPrediction?> PredictAsync(string text)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/predict", new { text });

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadFromJsonAsync<AiPrediction>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}