using System.Text.Json;

namespace mastodon_bot;

public abstract class ContentCreator
{
    public abstract string Url { get; init; }
    protected abstract Dictionary<string, string> GetParameters(DateTime dateTime);

    public abstract Task<string> FetchToToot(DateTime dateTime);

    protected FetcherBase FetcherBase { get; init; }

    protected async Task<string> FetchAsync(DateTime dateTime)
    {
        return await FetcherBase.FetchAsync(Url, GetParameters(dateTime));
    }
}

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

    protected override Dictionary<string, string> GetParameters(DateTime dateTime)
    {
        var reportTime = GetReportTime(dateTime);
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

    public override async Task<string> FetchToToot(DateTime dateTime)
    {
        var contentYesterday = await FetchAsync(DateTime.Now.AddDays(-1));
        using var jsonYesterday = JsonDocument.Parse(contentYesterday);

        var contentToday = await FetchAsync(DateTime.Now);
        using var jsonToday = JsonDocument.Parse(contentToday);

        var toot = ToToot(jsonYesterday, jsonToday);
        return toot;
    }


    private string ToToot(JsonDocument yesterdayJson, JsonDocument todayJson)
    {
        string GetMinMaxTempComparisonText(DateTime dateTime, List<(DateTime, WeatherContent)> valueTuples,
            DateTime today1, List<(DateTime, WeatherContent)> list)
        {
            string GetComparison(string title, float prev, float next)
            {
                if (Math.Abs(prev - next) < 0.01f)
                {
                    return $"{title}: {prev}℃";
                }
                else
                {
                    var arrow = (prev < next) ? "↗" : "↘";
                    return $"{title}: {prev}℃ {arrow} {next}℃";
                }
            }

            // 어제 데이터와 오늘에서 어제 기록 중, 일 최저기온과 일 최고기온을 추출한다.
            var yesterdayMinMaxTemp = GetMinMaxTemp(dateTime, valueTuples);
            var todayMinMaxTemp = GetMinMaxTemp(today1, list);

            if (yesterdayMinMaxTemp.Count != 2 || todayMinMaxTemp.Count != 2)
            {
                Logger.LogError("어제나 오늘의 최저기온과 최고기온이 모두 존재하지 않습니다.");
                return "";
            }

            var yesterdayMin = yesterdayMinMaxTemp[WeatherCategoryType.DailyMinTemperature];
            var yesterdayMax = yesterdayMinMaxTemp[WeatherCategoryType.DailyMaxTemperature];
            var todayMin = todayMinMaxTemp[WeatherCategoryType.DailyMinTemperature];
            var todayMax = todayMinMaxTemp[WeatherCategoryType.DailyMaxTemperature];

            var s = $"{GetComparison("최저기온", yesterdayMin, todayMin)} {GetComparison("최고기온", yesterdayMax, todayMax)}";
            return s;
        }

        Dictionary<WeatherCategoryType, float> GetMinMaxTemp(DateTime timeToCheck,
            List<(DateTime Key, WeatherContent Value)> weatherContents)
        {
            return weatherContents.Where(pair => pair.Key.Date == timeToCheck.Date)
                .Where(pair =>
                    pair.Value.Category.Type is WeatherCategoryType.DailyMinTemperature
                        or WeatherCategoryType.DailyMaxTemperature)
                .GroupBy(pair => pair.Value.Category.Type)
                .Select(group => (group.Key, group.Average(pair => float.Parse(pair.Value.ForecastValue))))
                .ToDictionary(tuple => tuple.Key, tuple => tuple.Item2);
        }

        var today = DateTime.Now;
        var yesterday = DateTime.Now.AddDays(-1);

        var yesterdayData = ToWeatherContents(yesterdayJson);
        var todayData = ToWeatherContents(todayJson);

        var reportDate = todayData.First().Item2.BaseDateTime;
        var reportDateString = reportDate.ToString("(M월 d일 H시 기상청 일기예보 기준)\n");

        // TODO: 이 절망적인 나열을 개선하자.
        var minMaxTempComparisonText = GetMinMaxTempComparisonText(yesterday, yesterdayData, today, todayData);

        // 오늘의 날씨를 요약한다. 오늘로부터 09시, 19시의 강수확률과 강수량을 최대 6회까지 추출한다.
        var todayWeatherSummaryData = todayData.Where(pair => pair.Item1.Hour is 9 or 19)
            .Where(pair => pair.Item2.Category.Type is WeatherCategoryType.RainProbability
                or WeatherCategoryType.RainPattern or WeatherCategoryType.RainPerHour
                or WeatherCategoryType.SnowPerHour)
            .GroupBy(pair => pair.Item1)
            .Select(group => new WeatherSlice(group))
            .Take(6);


        var rainDataBetween9And19ByDate = todayData.Where(pair => pair.Item1.Hour is > 9 and < 19)
            .Where(pair => pair.Item2.Category.Type is WeatherCategoryType.RainProbability
                or WeatherCategoryType.RainPattern or WeatherCategoryType.RainPerHour
                or WeatherCategoryType.SnowPerHour)
            .GroupBy(pair => pair.Item1) // 시간별로 그루핑
            .Where(group =>
            {
                return group.Any(pair => pair.Item2.Category.Type is WeatherCategoryType.RainPattern
                                         && pair.Item2.ForecastValue is "1" or "2" or "3" or "4" or "5" or "6" or "7"
                                             or "8" or "9" or "10"); //  비나 눈, 소나기
            })
            .Select(group => new WeatherShortSlice(group))
            .GroupBy(slice => slice.ForecastDateTime.Date)
            .ToDictionary(group => group.Key, group => group.ToList()); // 다시 날짜별로 그루핑

        // 오전 9시 -> 그 사이의 강수량 -> 오후 7시 순으로 표시한다.
        // 이미 9시를 지났다면, 강수량 -> 오후 7시 -> 다음날 오전 9시 순으로 표시한다.
        // 그 사이의 강수확률이 있을 경우, "11시(10%), 12시(60%), 13시(30%)"와 같이 표시한다.

        var combinedRainData = new Dictionary<DateTime, List<string>>();

        void TryAddOrCreate(WeatherSlice slice)
        {
            if (combinedRainData.TryGetValue(slice.ForecastDateTime.Date, out var rainData))
            {
                rainData.Add(slice.ToString());
            }
            else
            {
                combinedRainData.Add(slice.ForecastDateTime.Date, new List<string> { slice.ToString() });
            }
        }

        foreach (var weatherSlice in todayWeatherSummaryData)
        {
            if (weatherSlice.ForecastDateTime.Hour < 12)
            {
                TryAddOrCreate(weatherSlice);

                continue;
            }

            var rainDataBetween9And19 = rainDataBetween9And19ByDate.FirstOrDefault(group =>
                group.Key == weatherSlice.ForecastDateTime.Date);
            if (rainDataBetween9And19.Value != null)
            {
                var joinedData = string.Join(" | ", rainDataBetween9And19.Value);
                if (combinedRainData.TryGetValue(weatherSlice.ForecastDateTime.Date, out var rainData))
                {
                    rainData.Add(joinedData);
                }
                else
                {
                    combinedRainData.Add(weatherSlice.ForecastDateTime.Date, new List<string>() { joinedData });
                }
            }

            TryAddOrCreate(weatherSlice);
        }

        var todayWeatherSummaryText =
            "[일기예보]\n" + string.Join("\n\n", combinedRainData.Select(pair => string.Join("\n", pair.Value)));

        var result = reportDateString + minMaxTempComparisonText + "\n" + todayWeatherSummaryText;

        return result.Trim();
    }

    private List<(DateTime, WeatherContent)> ToWeatherContents(JsonDocument jsonDocument)
    {
        var items = jsonDocument.RootElement.GetProperty("response").GetProperty("body").GetProperty("items")
            .GetProperty("item");
        var test = items.EnumerateArray();

        return (test.Select(itemElement => itemElement.Deserialize<WeatherContentRaw>())
                .Where(weatherContentRaw => weatherContentRaw != null)
                .Select(weatherContentRaw => new WeatherContent(weatherContentRaw)))
            .Select(content => (content.ForecastDateTime, content)).ToList();
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

public class WeatherReportContentCreator : ContentCreator
{
    public override string Url { get; init; } = Constants.WeatherReportUrl;

    private readonly string _serviceKey;

    public WeatherReportContentCreator(string serviceKey, FetcherBase fetcherBase)
    {
        _serviceKey = serviceKey;
        FetcherBase = fetcherBase;
    }

    protected override Dictionary<string, string> GetParameters(DateTime dateTime)
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

    public override async Task<string> FetchToToot(DateTime dateTime)
    {
        var content = await FetchAsync(DateTime.Now);
        using var json = JsonDocument.Parse(content);
        var toot = ToToot(json);
        return toot;
    }


    private string ToToot(JsonDocument content)
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
            report = report.Trim();
            var reportTrimmed = report[..Math.Min(report.Length, Constants.MaxTootLengthWithMargin)];
            if (report.Length != reportTrimmed.Length)
            {
                reportTrimmed += "...";
            }

            var toot = $"기상청 발표 기상시황({time} 발표):\n{reportTrimmed}";
            Logger.Log(toot);
            toot = toot.Trim();
            return toot;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}