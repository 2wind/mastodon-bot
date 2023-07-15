using Microsoft.AspNetCore.WebUtilities;

namespace mastodon_bot;

public abstract class FetcherBase
{
    public abstract Task<string> FetchAsync(string url, IDictionary<string, string> query);

    public static FetcherBase CreateFetcher(bool isLocal, HttpClient httpClient, int maxRetry, float delay)
    {
        return isLocal ? new LocalFetcherBase() : new Fetcher(httpClient, maxRetry, delay);
    }
}

public class Fetcher : FetcherBase
{
    private readonly HttpClient _httpClient;
    private readonly int _maxRetry;
    private readonly float _delay;

    public Fetcher(HttpClient httpClient, int maxRetry, float delay)
    {
        _httpClient = httpClient;
        _maxRetry = maxRetry;
        _delay = delay;
    }

    public override async Task<string> FetchAsync(string url, IDictionary<string, string> query)
    {
        var tryCount = 0;
        var success = false;
        var content = string.Empty;
        while (!success && tryCount < _maxRetry)
        {
            try
            {
                var fullUrl = QueryHelpers.AddQueryString(url, query);
                if (fullUrl == null)
                {
                    throw new Exception("쿼리를 만들 수 없습니다.");
                }

                var response = await _httpClient.GetAsync(fullUrl);
                content = response.Content.ReadAsStringAsync().Result;
                Logger.Log($"Successfully fetched content! length {content.Length}");
                Logger.Log($"Content: {content[0..Math.Min(content.Length, 100)]}...");
                success = true;
                return content;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                Logger.Log($"Retrying after {_delay}...({tryCount} / {_maxRetry}))");
                if (tryCount >= _maxRetry)
                {
                    throw;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_delay));
            tryCount++;
        }

        return content;
    }
}

public class LocalFetcherBase : FetcherBase
{
    private static readonly Dictionary<string, string> _urlToLocalPath = new()
    {
        { Constants.WeatherUrl, "test/sample.json" },
        { Constants.WeatherReportUrl, "test/sample2.json" }
    };

    public override Task<string> FetchAsync(string url, IDictionary<string, string> query)
    {
        if (_urlToLocalPath.TryGetValue(url, out var path))
        {
            return Task.FromResult(File.ReadAllText(path));
        }

        throw new Exception("지원하지 않는 URL입니다.");
    }
}