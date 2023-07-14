﻿using System.Text.Json;
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


            [Option('n', "notoot", Required = false, HelpText = "Do not toot.")]
            public bool NoToot { get; set; }
        }

        static void Main(string[] args)
        {
            var isLocal = false;
            var noToot = false;
            Parser.Default.ParseArguments<Options>(args).WithParsed(options =>
            {
                isLocal = options.IsLocal;
                noToot = options.NoToot;
                Logger.IsVerbose = options.Verbose;
            });

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

            var tooter = TooterBase.CreateTooter(noToot, provider.GetMastodonAccessToken(), provider.GetInstance(),
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
                    Logger.LogError(e);
                    continue;
                }
            }
        }

        private static async Task TryTootAsync(ContentCreator contentCreator, TooterBase tooterBase)
        {
            var toot = await contentCreator.FetchToToot(DateTime.Now);
            if (toot != string.Empty)
            {
                AddBotHashTags(ref toot);
                await tooterBase.MakeToot(toot);
            }
        }

        private static void AddBotHashTags(ref string toot)
        {
            toot += "\n\n#봇 #bot #날씨";
        }
    }
}