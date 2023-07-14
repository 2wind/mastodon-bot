namespace mastodon_bot;

public static class Logger
{
    public static bool IsVerbose { get; set; }

    public static void Log(string content)
    {
        if (!IsVerbose) return;
        Console.WriteLine(content);
    }

    public static void LogError(object content)
    {
        Console.WriteLine("ERROR: ");
        Console.WriteLine(content);
    }
}