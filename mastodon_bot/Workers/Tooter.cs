namespace mastodon_bot;

using Mastonet;

public abstract class TooterBase
{
    public abstract Task MakeToot(string toot);

    public static TooterBase CreateTooter(bool isLocal, string accessToken, string instance, HttpClient httpClient,
        int maxRetry, float delay)
    {
        return isLocal
            ? new TestTooter()
            : new Tooter(accessToken, instance, httpClient, maxRetry, delay);
    }

    public async Task MakeAsyncTootsBySchedule(List<ContentCreator> contentCreators)
    {
        try
        {
            var asyncToots = contentCreators.Select(creator => creator.FetchToToot(DateTime.Now)).ToArray();
            var toots = await Task.WhenAll(asyncToots);

            foreach (var toot in toots)
            {
                TryTootAsync(toot).Wait(3000);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }


    public async Task TryTootAsync(string toot)
    {
        if (toot != string.Empty)
        {
            AddBotHashTags(ref toot);
            AddBotReference(ref toot);
            await MakeToot(toot);
        }

        await Task.CompletedTask;
    }

    private static void AddBotHashTags(ref string toot)
    {
        toot += "\n\n#봇 #bot #날씨 ";
    }

    private static void AddBotReference(ref string toot)
    {
        toot += "(기상청 OpenAPI를 이용)";
    }
}

public class TestTooter : TooterBase
{
    public override Task MakeToot(string toot)
    {
        if (toot.Length > Constants.MaxTootLength)
        {
            Logger.LogError($"Toot is too long! {toot.Length}");
        }

        Console.WriteLine("Dummy tooter, no actual toot made!");
        Console.WriteLine($"Tooting :{toot}");
        return Task.CompletedTask;
    }
}

public class Tooter : TooterBase
{
    private readonly string _accessToken;
    private readonly string _instance;
    private MastodonClient _client;
    private readonly int _maxRetry;
    private readonly float _delay;

    public Tooter(string accessToken, string instance, HttpClient httpClient, int maxRetry, float delay)
    {
        _accessToken = accessToken;
        _instance = instance;
        _maxRetry = maxRetry;
        _delay = delay;
        _client = new MastodonClient(_instance, _accessToken, httpClient);
    }

    public override async Task MakeToot(string toot)
    {
        if (toot.Length > Constants.MaxTootLength)
        {
            throw new ArgumentException($"Toot is too long! {toot.Length}");
        }

        var tryCount = 0;
        var success = false;
        while (!success && tryCount < _maxRetry)
        {
            try
            {
                Logger.Log($"Tooting :{toot}");
                await _client.PublishStatus(toot, Visibility.Unlisted);
                success = true;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                Logger.Log($"Retrying after {_delay}...({tryCount} / {_maxRetry}))");
                if (tryCount >= _maxRetry)
                {
                    throw;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_delay));
            tryCount++;
        }
    }
}