﻿namespace mastodon_bot;

public static class Constants
{
    public const string FilePath = @"settings.toml";
    public const int MaxTootLength = 500;
    public const int MaxTootLengthWithMargin = 420;

    public const string WeatherUrl = @"https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getVilageFcst";

    public const string WeatherReportUrl = @"https://apis.data.go.kr/1360000/VilageFcstMsgService/getWthrSituation";
}