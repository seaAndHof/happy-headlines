using HappyHeadlines.Shared;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CommentService;

public class ProfanityClient(HttpClient http, ILogger<ProfanityClient> logger)
{
    // While ProfanityService can't be reached: a short built-in list, so the worst words are still masked.
    public static readonly string[] FallbackWords = ["damn", "crap", "idiot"];

    public async Task<string> FilterAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var response = await http.PostAsJsonAsync("filter", new FilterRequest(text), cancellationToken);
            response.EnsureSuccessStatusCode();
            var filtered = await response.Content.ReadFromJsonAsync<FilterResponse>(cancellationToken);
            return filtered!.Text;
        }
        catch (Exception e) when (e is BrokenCircuitException or TimeoutRejectedException or HttpRequestException)
        {
            // Open circuit, timeout or ProfanityService down: comments keep working, with the fallback list.
            logger.LogWarning("ProfanityService unavailable ({Reason}); masking with the fallback list", e.GetType().Name);
            return ProfanityFilter.Mask(text, FallbackWords);
        }
    }
}
