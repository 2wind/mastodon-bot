using Tomlyn;
using Tomlyn.Model;

namespace mastodon_bot;

public class Provider
{
    private static readonly Dictionary<string, Location> NameToLocation = new()
    {
        { "압구정", new Location("압구정", 61, 126) },
        {
            "관악", new Location("관악", 60, 125)
        }
    };

    private TomlTable _settings;

    public Provider()
    {
        var settingPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, Constants.FilePath);
        var text = File.ReadAllText(settingPath);
        _settings = Toml.ToModel(text);
    }

    public string GetServiceKey() => GetSettingKey<string>("serviceKey");
    public string GetInstance() => GetSettingKey<string>("instance");
    public string GetMastodonAccessToken() => GetSettingKey<string>("accessToken");

    public (int maxRetryCount, float delay) GetRetryInfo()
    {
        return (GetSettingKeyParse<int>("maxRetryCount"), GetSettingKeyParse<float>("delay"));
    }

    private T GetSettingKey<T>(string key) where T : class
    {
        var value = _settings[key] as T;
        if (value == null)
        {
            var message = $"설정 파일에 {key}가 없습니다.";
            Logger.LogError(message);
            throw new Exception(message);
        }

        return value;
    }

    private T GetSettingKeyParse<T>(string key) where T : IParsable<T>
    {
        if (!T.TryParse(_settings[key].ToString(), null, out var value))
        {
            var message = $"설정 파일에 {key}가 없습니다.";
            Logger.LogError(message);
            throw new Exception(message);
        }

        return value;
    }


    // TODO: LocationProvider로 분리하면 좋을 것 같다.
    public (int x, int y) GetPositionBasedOnTime(DateTime dateTime)
    {
        if (dateTime.Date.DayOfWeek < DayOfWeek.Saturday)
        {
            return NameToLocation["관악"].Position;
        }
        else
        {
            return NameToLocation["압구정"].Position;
        }
    }
}

public class Location
{
    public string Name { get; init; }
    public (int x, int y) Position { get; init; }

    public Location(string name, int x, int y)
    {
        Name = name;
        Position = (x, y);
    }
}