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

    public DateTime BaseDateTime { get; init; }
    public RainPatternType RainPattern { get; set; }
    public float RainProbability { get; set; }
    public string RainPerHour { get; set; }
    public string SnowPerHour { get; set; }

    public override string ToString()
    {
        var result = $"{BaseDateTime.Day}일 {BaseDateTime.Hour}시에 ";
        result += RainPattern switch
        {
            RainPatternType.None => "맑습니다.",
            RainPatternType.Rain =>
                $"비가 내릴 예정이며, 강수확률은 {RainProbability}% 입니다. 1시간 강수량은 {RainPerHour} 입니다.",
            RainPatternType.RainSnow =>
                $"눈과 비가 내릴 예정이며, 강수확률은 {RainProbability}% 입니다. 1시간 강수량은 {RainPerHour} 입니다. 1시간 신적설은 {SnowPerHour}입니다.",
            RainPatternType.Snow => $"눈이 내릴 예정이며, 강수확률은 {RainProbability}% 입니다. 1시간 신적설은 {SnowPerHour}입니다.",
            RainPatternType.Hail =>
                $"소나기가 내릴 예정이며, 강수확률은 {RainProbability}% 입니다.  1시간 강수량은 {RainPerHour} 입니다.",
        };
        return result;
    }
}