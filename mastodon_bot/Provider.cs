using Tomlyn;

namespace mastodon_bot;

public class Provider
{
    public static Dictionary<string, Location> _nameToLocation = new()
    {
        { "압구정", new Location("압구정", 61, 126) },
        {
            "관악", new Location("관악", 60, 125)
        }
    };

    public string? GetServiceKey()
    {
        var text = File.ReadAllText(Constants.FilePath);
        var settings = Toml.ToModel(text);

        var serviceKey = settings["serviceKey"] as string;
        return serviceKey;
    }

    public (int x, int y) GetPositionBasedOnTime(DateTime dateTime)
    {
        if (dateTime.Date.DayOfWeek < DayOfWeek.Saturday)
        {
            return _nameToLocation["관악"].Position;
        }
        else
        {
            return _nameToLocation["압구정"].Position;
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