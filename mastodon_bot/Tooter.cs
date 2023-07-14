namespace mastodon_bot;

using Mastonet;

public abstract class TooterBase
{
    public abstract Task MakeToot(string toot);

    public static TooterBase CreateTooter(bool isLocal, string accessToken, string instance, HttpClient httpClient)
    {
        return isLocal
            ? new TestTooter()
            : new Tooter(accessToken, instance, httpClient);
    }
}

public class TestTooter : TooterBase
{
    public override async Task MakeToot(string toot)
    {
        Console.WriteLine("Dummy tooter, no actual toot made!");
        Console.WriteLine($"Tooting :{toot}");
    }
}

public class Tooter : TooterBase
{
    private readonly string _accessToken;
    private readonly string _instance;
    private MastodonClient _client;

    public Tooter(string accessToken, string instance, HttpClient httpClient)
    {
        _accessToken = accessToken;
        _instance = instance;
        _client = new MastodonClient(_instance, _accessToken, httpClient);
    }

    public override async Task MakeToot(string toot)
    {
        Logger.Log($"Tooting :{toot}");
        await _client.PublishStatus(toot, Visibility.Unlisted);
    }
}