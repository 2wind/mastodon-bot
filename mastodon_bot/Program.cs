using System.Text.Json;
using CommandLine;

namespace mastodon_bot
{
    static class Program
    {
        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('l', "local", Required = false, HelpText = "Use Local data for testing.")]
            public bool IsLocal { get; set; }
        }

        static void Main(string[] args)
        {
            var isLocal = false;
            Parser.Default.ParseArguments<Options>(args).WithParsed(options => isLocal = options.IsLocal);

            var provider = new Provider();
            var serviceKey = provider.GetServiceKey();
            var position = provider.GetPositionBasedOnTime(DateTime.Now);

            var httpClient = new HttpClient();
            var fetcher = FetcherBase.CreateFetcher(isLocal, httpClient);

            var contentCreators = new List<ContentCreator>()
            {
                new WeatherContentCreator(serviceKey, position, fetcher),
                new WeatherReportContentCreator(serviceKey, fetcher)
            };

            var tooter = TooterBase.CreateTooter(isLocal, provider.GetMastodonAccessToken(), provider.GetInstance(),
                httpClient);

            // TODO 비동기 프로그래밍을 제대로 이용하기

            foreach (var creator in contentCreators)
            {
                try
                {
                    TryTootAsync(creator, tooter).Wait();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    continue;
                }
            }
        }

        private static async Task TryTootAsync(ContentCreator contentCreator, TooterBase tooterBase)
        {
            var content = await contentCreator.FetchAsync();
            using var json = JsonDocument.Parse(content);
            var toot = contentCreator.ToToot(json);
            if (toot != string.Empty)
            {
                await tooterBase.MakeToot(toot);
            }
        }
    }
}