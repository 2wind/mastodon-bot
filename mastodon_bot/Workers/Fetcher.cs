using Microsoft.AspNetCore.WebUtilities;

namespace mastodon_bot;

public abstract class FetcherBase
{
    public abstract Task<string> FetchAsync(string url, IDictionary<string, string> query);

    public static FetcherBase CreateFetcher(bool isLocal, HttpClient httpClient)
    {
        return isLocal ? new LocalFetcherBase() : new Fetcher(httpClient);
    }
}

public class Fetcher : FetcherBase
{
    private readonly HttpClient _httpClient;

    public Fetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<string> FetchAsync(string url, IDictionary<string, string> query)
    {
        var fullUrl = QueryHelpers.AddQueryString(url, query);
        if (fullUrl == null)
        {
            throw new Exception("쿼리를 만들 수 없습니다.");
        }

        var response = await _httpClient.GetAsync(fullUrl);
        var content = response.Content.ReadAsStringAsync().Result;

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