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
        var dayBeforeYesterday = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day - 2, 02, 11, 00);
        var yesterday = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day - 1, 02, 11, 00);
        var today2Am = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 02, 11, 00);

        File.Delete(ToDateFileName(dayBeforeYesterday));

        var yesterdayFileName = ToDateFileName(yesterday);
        JsonDocument yesterdayDocument;
        if (File.Exists(yesterdayFileName))
        {
            var text = await File.ReadAllTextAsync(yesterdayFileName);
            yesterdayDocument = JsonDocument.Parse(text);
        }
        else
        {
            yesterdayDocument = JsonDocument.Parse("{}");
        }

        var jsonDocuments = await Task.WhenAll(FetchAsyncToJson(today2Am), FetchAsyncToJson(dateTime));

        var toot = ToToot(yesterdayDocument, jsonDocuments[0], jsonDocuments[1]);

        await File.CreateText(ToDateFileName(dateTime)).WriteAsync(jsonDocuments[1].RootElement.GetRawText());

        foreach (var jsonDocument in jsonDocuments)
        {
            jsonDocument.Dispose();
        }

        return toot;
    }

    private static string ToDateFileName(DateTime dateTime)
    {
        return dateTime.ToString("yyyyMMdd") + ".json";
    }

    private async Task<JsonDocument> FetchAsyncToJson(DateTime dateTime)
    {
        var content = await FetchAsync(dateTime);
        var json = JsonDocument.Parse(content);
        return json;
    }


    private string ToToot(JsonDocument jsonYesterday, JsonDocument jsonToday2Am, JsonDocument jsonToday)
    {
        string GetMinMaxTempComparisonText(DateTime previous, List<(DateTime, WeatherContent)> previousValueTuples,
            DateTime current, List<(DateTime, WeatherContent)> currentValueTuples)
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
            var yesterdayMinMaxTemp = GetMinMaxTemp(previous, previousValueTuples);
            var todayMinMaxTemp = GetMinMaxTemp(current, currentValueTuples);

            if (todayMinMaxTemp.Count != 2)
            {
                Logger.LogError("어제나 오늘의 최저기온과 최고기온이 모두 존재하지 않습니다.");
                return "";
            }

            float todayMin, todayMax;

            if (yesterdayMinMaxTemp.Count != 2 && todayMinMaxTemp.Count == 2)
            {
                todayMin = todayMinMaxTemp[WeatherCategoryType.DailyMinTemperature];
                todayMax = todayMinMaxTemp[WeatherCategoryType.DailyMaxTemperature];
                return $"{GetComparison("최저기온", todayMin, todayMin)} {GetComparison("최고기온", todayMax, todayMax)}";
            }

            var yesterdayMin = yesterdayMinMaxTemp[WeatherCategoryType.DailyMinTemperature];
            var yesterdayMax = yesterdayMinMaxTemp[WeatherCategoryType.DailyMaxTemperature];
            todayMin = todayMinMaxTemp[WeatherCategoryType.DailyMinTemperature];
            todayMax = todayMinMaxTemp[WeatherCategoryType.DailyMaxTemperature];

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


        var yesterdayData = ToWeatherContents(jsonYesterday);
        var todayData = ToWeatherContents(jsonToday);

        if (todayData.Count == 0)
        {
            return string.Empty;
        }

        var reportDate = todayData.First().Item2.BaseDateTime;
        var reportDateString = reportDate.ToString("(M월 d일 H시 기상청 일기예보 기준)\n");

        var minMaxTempComparisonText =
            GetMinMaxTempComparisonText(yesterday, yesterdayData, today, ToWeatherContents(jsonToday2Am));

        var todayWeatherSummaryData = TodayWeatherSummaryData(todayData);
        var rainDataBetween9And19ByDate = RainDataBetween9And19ByDate(todayData);
        var rainDataAfter19AndBefore9ByDate = RainDataAfter19AndBefore9(todayData);

        // 오전 9시 -> 그 사이의 강수량 -> 오후 7시 순으로 표시한다.
        // 이미 9시를 지났다면, 강수량 -> 오후 7시 -> 다음날 오전 9시 순으로 표시한다.
        // 그 사이의 강수확률이 있을 경우, "11시(10%), 12시(60%), 13시(30%)"와 같이 표시한다.
        // 오후 7시 뒤에 강수확률이 있다면 붙여서 표기한다. 

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

        void AddRainDataFromRainOnlyData(Dictionary<DateTime, List<WeatherShortSlice>> rainOnlyDataByDate,
            WeatherSlice slice)
        {
            var rainDataBetween9And19 = rainOnlyDataByDate.FirstOrDefault(group =>
                group.Key == slice.ForecastDateTime.Date);
            if (rainDataBetween9And19.Value != null)
            {
                var joinedData = string.Join(" | ", rainDataBetween9And19.Value);
                if (combinedRainData.TryGetValue(slice.ForecastDateTime.Date, out var rainData))
                {
                    rainData.Add(joinedData);
                }
                else
                {
                    combinedRainData.Add(slice.ForecastDateTime.Date, new List<string>() { joinedData });
                }
            }
        }

        foreach (var weatherSlice in todayWeatherSummaryData)
        {
            if (weatherSlice.ForecastDateTime.Hour < 12)
            {
                TryAddOrCreate(weatherSlice);

                continue;
            }

            AddRainDataFromRainOnlyData(rainDataBetween9And19ByDate, weatherSlice);

            TryAddOrCreate(weatherSlice);

            AddRainDataFromRainOnlyData(rainDataAfter19AndBefore9ByDate, weatherSlice);
        }

        var todayWeatherSummaryText =
            "[일기예보]\n" + string.Join("\n\n", combinedRainData.Select(pair => string.Join("\n", pair.Value)));

        var result = reportDateString + minMaxTempComparisonText + "\n" + todayWeatherSummaryText;

        return result.Trim();
    }

    private static Dictionary<DateTime, List<WeatherShortSlice>> RainDataBetween9And19ByDate(
        List<(DateTime, WeatherContent)> todayData)
    {
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
        return rainDataBetween9And19ByDate;
    }

    private static Dictionary<DateTime, List<WeatherShortSlice>> RainDataAfter19AndBefore9(
        List<(DateTime, WeatherContent)> todayData)
    {
        var rainDataAfter19AndBefore9 = todayData.Where(pair => pair.Item1.Hour is > 19 or < 9)
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
            .GroupBy(slice =>
            {
                var date = slice.ForecastDateTime.Date;
                if (slice.ForecastDateTime.Hour < 9)
                {
                    date = date.AddDays(-1);
                }

                return date;
            }) // 18일 20시와 19일 6시를 모두 18일로 묶는다.
            .ToDictionary(group => group.Key, group => group.ToList()); // 다시 날짜별로 그루핑
        return rainDataAfter19AndBefore9;
    }

    private static IEnumerable<WeatherSlice> TodayWeatherSummaryData(List<(DateTime, WeatherContent)> todayData)
    {
        // 오늘의 날씨를 요약한다. 오늘로부터 09시, 19시의 강수확률과 강수량을 최대 6회까지 추출한다.
        var todayWeatherSummaryData = todayData.Where(pair => pair.Item1.Hour is 9 or 19)
            .Where(pair => pair.Item2.Category.Type is WeatherCategoryType.RainProbability
                or WeatherCategoryType.RainPattern or WeatherCategoryType.RainPerHour
                or WeatherCategoryType.SnowPerHour)
            .GroupBy(pair => pair.Item1)
            .Select(group => new WeatherSlice(group))
            .Take(6);
        return todayWeatherSummaryData;
    }

    private List<(DateTime, WeatherContent)> ToWeatherContents(JsonDocument jsonDocument)
    {
        try
        {
            if (!jsonDocument.RootElement.TryGetProperty("response", out var response))
            {
                Logger.Log("해당하는 내용이 없으므로 넘어갑니다.");
                return [];
            }

            var items = response.GetProperty("body").GetProperty("items")
                .GetProperty("item");
            var test = items.EnumerateArray();

            return [.. test.Select(itemElement => itemElement.Deserialize<WeatherContentRaw>())
                    .Where(weatherContentRaw => weatherContentRaw != null)
                    .Select(static ct => new WeatherContent(ct))
                .Select(content => (content.ForecastDateTime, content))];
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            return [];
        }
    }

    public static DateTime GetReportTime(DateTime dateTime)
    {
        var shortReportHours = new int[] { 23, 20, 17, 14, 11, 8, 5, 2 };
        foreach (var reportHour in shortReportHours)
        {
            if (dateTime.Hour < reportHour) continue;
            if (dateTime.Hour == reportHour && dateTime.Minute <= 10) continue;

            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, reportHour, 0, 0);
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