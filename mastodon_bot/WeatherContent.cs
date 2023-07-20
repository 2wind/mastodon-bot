using System.Globalization;

namespace mastodon_bot;

public record WeatherContentRaw(
    string baseDate,
    string baseTime,
    string category,
    string fcstDate,
    string fcstTime,
    string fcstValue,
    int nx,
    int ny
);

// 각잡고 하려면 타입별로 value를 잘 파싱하는 파생 클래스들을 쭉 만들면 된다.
public enum WeatherCategoryType
{
    None, // 에러 났을 때의 기록
    RainProbability, // POP
    RainPattern, // PTY
    RainPerHour, // PCP
    Humidity, // REH
    SnowPerHour, // SNO
    SkyPattern, // SKY
    TemperaturePerHour, // TMP
    DailyMinTemperature, // TMN
    DailyMaxTemperature, // TMX
    WindSpeedHorizontal, // UUU
    WindSpeedVertical, // VVV
    WaveHeight, // WAV
    WindVector, // VEC
    WindSpeed, // WSD
}

public struct WeatherCategory
{
    public WeatherCategoryType Type { get; init; }
    public string Name { get; init; }
    public string Unit { get; init; }

    private WeatherCategory(WeatherCategoryType type, string name, string unit)
    {
        Type = type;
        Name = name;
        Unit = unit;
    }

    public static readonly Dictionary<string, WeatherCategoryType> RawTypeToType = new()
    {
        { "POP", WeatherCategoryType.RainProbability },
        { "PTY", WeatherCategoryType.RainPattern },
        { "PCP", WeatherCategoryType.RainPerHour },
        { "REH", WeatherCategoryType.Humidity },
        { "SNO", WeatherCategoryType.SnowPerHour },
        { "SKY", WeatherCategoryType.SkyPattern },
        { "TMP", WeatherCategoryType.TemperaturePerHour },
        { "TMN", WeatherCategoryType.DailyMinTemperature },
        { "TMX", WeatherCategoryType.DailyMaxTemperature },
        { "UUU", WeatherCategoryType.WindSpeedHorizontal },
        { "VVV", WeatherCategoryType.WindSpeedVertical },
        { "WAV", WeatherCategoryType.WaveHeight },
        { "VEC", WeatherCategoryType.WindVector },
        { "WSD", WeatherCategoryType.WindSpeed },
    };

    public static readonly Dictionary<WeatherCategoryType, WeatherCategory> TypeToCategory = new()
    {
        { WeatherCategoryType.None, new WeatherCategory(WeatherCategoryType.None, string.Empty, string.Empty) },
        { WeatherCategoryType.RainProbability, new WeatherCategory(WeatherCategoryType.RainProbability, "강수확률", "%") },
        { WeatherCategoryType.RainPattern, new WeatherCategory(WeatherCategoryType.RainPattern, "강수형태", "코드값") },
        {
            WeatherCategoryType.RainPerHour, new WeatherCategory(WeatherCategoryType.RainPerHour, "1시간 강수량", "(범주) 1mm")
        },
        { WeatherCategoryType.Humidity, new WeatherCategory(WeatherCategoryType.Humidity, "습도", "%") },
        {
            WeatherCategoryType.SnowPerHour, new WeatherCategory(WeatherCategoryType.SnowPerHour, "1시간 신적설", "(범주) 1mm")
        },
        { WeatherCategoryType.SkyPattern, new WeatherCategory(WeatherCategoryType.SkyPattern, "하늘상태", "코드값") },
        {
            WeatherCategoryType.TemperaturePerHour,
            new WeatherCategory(WeatherCategoryType.TemperaturePerHour, "1시간 기온", "℃")
        },
        {
            WeatherCategoryType.DailyMinTemperature,
            new WeatherCategory(WeatherCategoryType.DailyMinTemperature, "아침 최저기온", "℃")
        },
        {
            WeatherCategoryType.DailyMaxTemperature,
            new WeatherCategory(WeatherCategoryType.DailyMaxTemperature, "낮 최고기온", "℃")
        },
        {
            WeatherCategoryType.WindSpeedHorizontal,
            new WeatherCategory(WeatherCategoryType.WindSpeedHorizontal, "풍속(동서성분)", "m/s")
        },
        {
            WeatherCategoryType.WindSpeedVertical,
            new WeatherCategory(WeatherCategoryType.WindSpeedVertical, "풍속(남북성분)", "m/s")
        },
        { WeatherCategoryType.WaveHeight, new WeatherCategory(WeatherCategoryType.WaveHeight, "파고", "M") },
        { WeatherCategoryType.WindVector, new WeatherCategory(WeatherCategoryType.WindVector, "풍향", "deg") },
        { WeatherCategoryType.WindSpeed, new WeatherCategory(WeatherCategoryType.WindSpeed, "풍속", "m/s") }
    };
}

public class WeatherContent
{
    public DateTime BaseDateTime { get; init; }
    public DateTime ForecastDateTime { get; init; }
    public WeatherCategory Category { get; init; }
    public string ForecastValue { get; init; }
    public Location Location { get; init; }

    public WeatherContent(WeatherContentRaw weatherContentRaw)
    {
        BaseDateTime = DateTime.ParseExact($"{weatherContentRaw.baseDate} {weatherContentRaw.baseTime}",
            "yyyyMMdd HHmm", null);
        ForecastDateTime = DateTime.ParseExact($"{weatherContentRaw.fcstDate} {weatherContentRaw.fcstTime}",
            "yyyyMMdd HHmm", null);

        Category = WeatherCategory.RawTypeToType.TryGetValue(weatherContentRaw.category, out var categoryType)
            ? WeatherCategory.TypeToCategory[categoryType]
            : WeatherCategory.TypeToCategory[WeatherCategoryType.None];
        ForecastValue = weatherContentRaw.fcstValue;
        Location = new Location(string.Empty, weatherContentRaw.nx, weatherContentRaw.ny);
    }
}

public class WeatherSlice
{
    public enum RainPatternType
    {
        None,
        Rain,
        RainSnow,
        Snow,
        Hail,
    }

    public DateTime ForecastDateTime { get; init; }
    public RainPatternType RainPattern { get; init; }
    public float RainProbability { get; init; }
    public string RainPerHour { get; init; }
    public string SnowPerHour { get; init; }

    public WeatherSlice(IGrouping<DateTime, (DateTime, WeatherContent)> weatherData)
    {
        ForecastDateTime = weatherData.Key;
        foreach (var weatherContent in weatherData)
        {
            switch (weatherContent.Item2.Category.Type)
            {
                case WeatherCategoryType.RainProbability:
                {
                    RainProbability = float.Parse(weatherContent.Item2.ForecastValue);
                    break;
                }
                case WeatherCategoryType.RainPattern:
                {
                    RainPattern =
                        (RainPatternType)int.Parse(weatherContent.Item2.ForecastValue);
                    break;
                }
                case WeatherCategoryType.RainPerHour:
                {
                    RainPerHour = weatherContent.Item2.ForecastValue;
                    break;
                }
                case WeatherCategoryType.SnowPerHour:
                {
                    SnowPerHour = weatherContent.Item2.ForecastValue;
                    break;
                }
            }
        }
    }

    public override string ToString()
    {
        var result = ForecastDateTime.ToString("M월 d일(ddd) tt h시", new CultureInfo("ko-KR")) + ": ";
        result += RainPattern switch
        {
            RainPatternType.None => "☀️맑음",
            RainPatternType.Rain =>
                $"🌧️비: ({RainProbability}%, {RainPerHour})",
            RainPatternType.RainSnow =>
                $"🌨️눈과 비: ({RainProbability}%, 비 {RainPerHour}, 눈 {SnowPerHour}",
            RainPatternType.Snow => $"❄️눈: ({RainProbability}%, {SnowPerHour})",
            RainPatternType.Hail =>
                $"⛈️소나기: ({RainProbability}%, {RainPerHour})",
        };
        return result;
    }
}

class WeatherShortSlice : WeatherSlice
{
    public WeatherShortSlice(IGrouping<DateTime, (DateTime, WeatherContent)> weatherData) : base(weatherData)
    {
    }

    public override string ToString()
    {
        var result = ForecastDateTime.ToString("h시");
        result += RainPattern switch
        {
            RainPatternType.None => "☀️맑음",
            RainPatternType.Rain =>
                $"🌧️비({RainProbability}%, {RainPerHour})",
            RainPatternType.RainSnow =>
                $"🌨️눈과 비({RainProbability}%, 비 {RainPerHour}, 눈 {SnowPerHour}",
            RainPatternType.Snow => $"❄️눈({RainProbability}%, {SnowPerHour})",
            RainPatternType.Hail =>
                $"⛈️소나기({RainProbability}%, {RainPerHour})",
        };
        return result;
    }
}