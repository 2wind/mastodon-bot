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
        var text = File.ReadAllText(Constants.FilePath);
        _settings = Toml.ToModel(text);
    }

    public string? GetServiceKey()
    {
        var serviceKey = _settings["serviceKey"] as string;
        return serviceKey;
    }

    public string? GetMastodonAccessToken()
    {
        var accessToken = _settings["mastodon"] as string;
        return accessToken;
    }

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