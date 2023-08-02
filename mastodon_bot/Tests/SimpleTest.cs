using RichardSzalay.MockHttp;

namespace mastodon_bot.Tests;

using Xunit;

public class SimpleTest
{
    [Fact]
    public void Test1()
    {
        Assert.True(true);
    }

    [Fact]
    public void TooterTest()
    {
        var tooter = TooterBase.CreateTooter(true, "1234", "https://example.org/toot", null, 0, 0);
        Assert.NotNull(tooter);

        tooter.TryTootAsync("test").Wait();

        tooter.TryTootAsync(string.Empty).Wait();

        tooter.TryTootAsync(StringLargerThan500).Wait();
    }

    static readonly string StringLargerThan500 = new string('a', 501);

    public static IEnumerable<object[]> TootExamples =>
        new List<object[]>
        {
            new object[] { "t", "t" },
            new object[] { StringLargerThan500, StringLargerThan500[..500] },
        };

    [Theory]
    [InlineData("test", "test")]
    [MemberData(nameof(TootExamples))]
    public async Task RealTooterTest(string toot, string actualToot)
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://example.org/toot/api/v1/statuses")
            .WithFormData("status", actualToot)
            .Respond("application/json", "{'id': 1, 'content': 'Hello World'}");


        var httpClient = mockHttp.ToHttpClient();

        var tooter = TooterBase.CreateTooter(false, "1234", "https://example.org/toot", httpClient, 5, 1.0f);
        Assert.NotNull(tooter);

        await tooter.TryTootAsync(toot);
    }

    public static IEnumerable<object[]> times => new List<object[]>()
    {
        new object[] { new DateTime(2023, 8, 1, 01, 00, 00), new DateTime(2023, 7, 31, 23, 00, 00) },
        new object[] { new DateTime(2023, 8, 1, 02, 00, 00), new DateTime(2023, 7, 31, 23, 00, 00) },
        new object[] { new DateTime(2023, 8, 1, 02, 09, 00), new DateTime(2023, 7, 31, 23, 00, 00) },
        new object[] { new DateTime(2023, 8, 1, 02, 10, 00), new DateTime(2023, 7, 31, 23, 00, 00) },

        new object[] { new DateTime(2023, 8, 1, 02, 11, 00), new DateTime(2023, 8, 1, 02, 00, 00) },
        new object[] { new DateTime(2023, 8, 1, 03, 00, 00), new DateTime(2023, 8, 1, 02, 00, 00) },
        new object[] { new DateTime(2023, 8, 1, 04, 00, 00), new DateTime(2023, 8, 1, 02, 00, 00) },
        new object[] { new DateTime(2023, 8, 1, 05, 00, 00), new DateTime(2023, 8, 1, 02, 00, 00) },

        new object[] { new DateTime(2023, 8, 1, 05, 20, 00), new DateTime(2023, 8, 1, 05, 00, 00) },
        new object[] { new DateTime(2023, 8, 1, 06, 00, 00), new DateTime(2023, 8, 1, 05, 00, 00) },
    };

    [Theory]
    [MemberData(nameof(times))]
    public void GetReportTimeTest(DateTime now, DateTime expected)
    {
        var reportTime = WeatherContentCreator.GetReportTime(now);
        Assert.Equal(expected, reportTime);
    }
}