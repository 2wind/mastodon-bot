namespace mastodon_bot;

using Mastonet;

public class Tooter
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

    public async Task MakeToot(string toot)
    {
        Console.WriteLine($"Tooting :{toot}");
        await _client.PublishStatus(toot, Visibility.Unlisted);
    }
}