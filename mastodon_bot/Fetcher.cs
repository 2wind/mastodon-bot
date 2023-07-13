using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace mastodon_bot;

public abstract class Fetcher
{
    public abstract string Url { get; init; }

    protected abstract Dictionary<string, string> GetParameters();

    public virtual string ToToot(string content) => string.Empty;

    private readonly HttpClient _httpClient;

    protected Fetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> FetchAsync()
    {
        var query = GetParameters();
        var fullUrl = QueryHelpers.AddQueryString(Url, query);
        if (fullUrl == null)
        {
            throw new Exception("쿼리를 만들 수 없습니다.");
        }

        var response = await _httpClient.GetAsync(fullUrl);
        var content = response.Content.ReadAsStringAsync().Result;

        return content;
    }
}

public class WeatherFetcher : Fetcher
{
    public override string Url { get; init; } =
        @"https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getVilageFcst";

    private readonly string _serviceKey;
    private readonly (int x, int y) _position;

    public WeatherFetcher(string serviceKey, (int x, int y) position, HttpClient httpClient) : base(httpClient)
    {
        _serviceKey = serviceKey;
        _position = position;
    }

    protected override Dictionary<string, string> GetParameters()
    {
        var reportTime = GetReportTime(DateTime.Now);
        return new Dictionary<string, string>()
        {
            { "serviceKey", _serviceKey },
            { "pageNo", "1" },
            { "numOfRows", "1000" },
            { "dataType", "JSON" },
            { "base_date", reportTime.ToString("yyyyMMdd") },
            { "base_time", reportTime.ToString("HHmm") },
            { "nx", _position.x.ToString() },
            { "ny", _position.y.ToString() },
        };
    }

    private DateTime GetReportTime(DateTime dateTime)
    {
        var shortReportHours = new int[] { 23, 20, 17, 14, 11, 8, 5, 2 };
        foreach (var reportHour in shortReportHours)
        {
            if (dateTime.Hour > reportHour && dateTime.Minute > 10)
            {
                return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, reportHour, 0, 0);
            }
        }

        // 만약 2시 10분 이전이라면 여기에 걸릴 것이며, 전날 23시로 돌아가야 한다.
        var yesterday = dateTime.AddDays(-1);
        return new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 23, 0, 0);
    }
}

public class WeatherReportFetcher : Fetcher
{
    public override string Url { get; init; } = "https://apis.data.go.kr/1360000/VilageFcstMsgService/getWthrSituation";
    private readonly string _serviceKey;

    public WeatherReportFetcher(string serviceKey, HttpClient httpClient) : base(httpClient)
    {
        _serviceKey = serviceKey;
    }

    protected override Dictionary<string, string> GetParameters()
    {
        return new Dictionary<string, string>()
        {
            { "serviceKey", _serviceKey },
            { "pageNo", "1" },
            { "numOfRows", "100" },
            { "dataType", "JSON" },
            { "stnId", "109" }, // 서울
        };
    }

    public override string ToToot(string content)
    {
        // body.items.item[0].wfSv1 을 출력하면 됨
        try
        {
            using var json = JsonDocument.Parse(content);
            var mainContent = json.RootElement.GetProperty("response").GetProperty("body").GetProperty("items")
                .GetProperty("item")[0];
            var rawTime = mainContent.GetProperty("tmFc").GetInt64().ToString();

            var time = new DateTime(int.Parse(rawTime[0..4]), int.Parse(rawTime[4..6]), int.Parse(rawTime[6..8]),
                int.Parse(rawTime[8..10]), int.Parse(rawTime[10..12]), 0);

            var report = mainContent.GetProperty("wfSv1").GetString();

            if (report == null) return string.Empty;

            var toot = $"기상청 발표 기상시황({time} 발표):\n{report}";
            // Console.WriteLine(toot);
            return toot;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}