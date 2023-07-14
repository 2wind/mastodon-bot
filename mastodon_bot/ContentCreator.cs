using System.Text.Json;

namespace mastodon_bot;

public abstract class ContentCreator
{
    public abstract string Url { get; init; }
    public abstract Dictionary<string, string> GetParameters();
    public abstract string ToToot(JsonDocument content);

    protected FetcherBase FetcherBase { get; init; }

    public async Task<string> FetchAsync()
    {
        return await FetcherBase.FetchAsync(Url, GetParameters());
    }
}

// TODO JSON 파싱하기
// JSON 객체 상태에서 작업할지, C# 객체 상태에서 작업할지 결정하기

// 각 Item은 baseData, baseTime, category, fcstDate, fcstTime, fcstValue, nx, ny를 가지고 있음
// baseData, baseTime : 일기예보를 한 날짜와 시간
// nx, ny : 예보 지점의 x, y 좌표 (보낸 것과 동일할 것이다.)

// fcstData, fcstTime : 예보의 대상이 되는 날짜와 시간
// category : 예보 항목, fcstValue : 예보 값
// 예보 항목은 POP(강수확률), PTY(강수형태), R06(6시간 강수량), REH(습도), S06(6시간 신적설), SKY(하늘상태), T3H(3시간 기온), TMN(아침 최저기온), TMX(낮 최고기온), UUU(풍속(동서성분)), VVV(풍속(남북성분)), WAV(파고), VEC(풍향), WSD(풍속)가 있음
// 각 항목의 단위는 각각 %, 코드값, 벡터, %, 벡터, 코드값, ℃, ℃, ℃, m/s, m/s, M, deg, m/s
// 코드값은 별도로 Dict를 만드는 것이 좋겠다.

// (fcstData, fcstTime)별로 묶은 자료구조 하나 (여러 (category, fcstValue)를 가질 것이다.)
// category별로 묶은 자료구조 하나 (여러 (fcstData, fcstTime, fcstValue)를 가질 것이다.)
// private 생성자로 넣고, human readable한 프로퍼티로 접근하자.

//Console.WriteLine(weatherContent);
public class WeatherContentCreator : ContentCreator
{
    public override string Url { get; init; } = Constants.WeatherUrl;


    private readonly string _serviceKey;
    private readonly (int x, int y) _position;

    public WeatherContentCreator(string serviceKey, (int x, int y) position, FetcherBase fetcherBase)
    {
        _serviceKey = serviceKey;
        _position = position;
        FetcherBase = fetcherBase;
    }

    public override Dictionary<string, string> GetParameters()
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

    public override string ToToot(JsonDocument content) => string.Empty;

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

public class WeatherReportContentCreator : ContentCreator
{
    public override string Url { get; init; } = Constants.WeatherReportUrl;

    private readonly string _serviceKey;

    public WeatherReportContentCreator(string serviceKey, FetcherBase fetcherBase)
    {
        _serviceKey = serviceKey;
        FetcherBase = fetcherBase;
    }

    public override Dictionary<string, string> GetParameters()
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

    public override string ToToot(JsonDocument content)
    {
        // body.items.item[0].wfSv1 을 출력하면 됨
        try
        {
            var mainContent = content.RootElement.GetProperty("response").GetProperty("body").GetProperty("items")
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