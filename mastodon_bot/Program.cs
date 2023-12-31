﻿using System.Globalization;
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


            [Option('n', "notoot", Required = false, HelpText = "Do not toot.")]
            public bool NoToot { get; set; }
        }

        static async Task Main(string[] args)
        {
            CultureInfo.CurrentCulture = new CultureInfo("ko-KR");

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
            var position = provider.GetPositionBasedOnTime(DateTime.Now); // TODO: 시간에 따라 바뀌는 값이므로 갱신 필요.
            var (maxRetryCount, delay) = provider.GetRetryInfo();

            var httpClient = new HttpClient();
            var fetcher = FetcherBase.CreateFetcher(isLocal, httpClient, maxRetryCount, delay);

            var contentCreators = new List<ContentCreator>()
            {
                new WeatherContentCreator(serviceKey, position, fetcher),
                new WeatherReportContentCreator(serviceKey, fetcher)
            };

            var tooter = TooterBase.CreateTooter(noToot, provider.GetMastodonAccessToken(), provider.GetInstance(),
                httpClient, maxRetryCount, delay);

            // while (true) // TODO: 서버를 돌리는 데 이것보다 더 좋은 방법은?
            {
                // var tootInfos = CheckTootMentions(tooter);
                // RespondToTootMentions(tootInfos, tooter);

                if (true) // TODO: 특정 시간에만 실행되도록 변경. 
                {
                    await tooter.MakeAsyncTootsBySchedule(contentCreators);
                }
            }
        }
    }
}