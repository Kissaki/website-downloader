namespace kcode.website_downloader;

internal static class Program
{
    static int Main()
    {
        Console.Title = "Website Downloader";

        using var lFac = LoggerFactory.Create(ConfigureLogging);
        if (!ProgramArguments.Parse(lFac, out var args)) return ReturnCode.Error;

        var log = lFac.CreateLogger(typeof(Program));
        var data = LoadWebsiteData(args.ReuseTargetFolder, args.TargetFolder, log);

        new CrawlingDownloader(args, data, lFac).StartCrawling();

        return ReturnCode.Success;

        static void ConfigureLogging(ILoggingBuilder c) => c
            .SetMinimumLevel(LogLevel.Debug)
            //.AddFilter("Microsoft", LogLevel.Warning)
            //.AddFilter("System", LogLevel.Warning)
            //.AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
            //.AddSimpleConsole()
            .AddSimpleConsole(c => c.SingleLine = true)
            .AddConsole()
            ;
    }

    private static WebsiteData LoadWebsiteData(bool reuseTargetFolder, string targetFolder, ILogger log)
    {
        if (!reuseTargetFolder || !Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
            // Fresh data
            return new()
            {
                FoundUrls = new HashSet<string>(),
                LocalUrlPaths = new HashSet<string>(),
                FilteredLocalUrlPaths = new HashSet<string>(),
                HandledLocalSubpaths = new HashSet<string>(),
                WrittenFiles = new HashSet<string>(),
                Redirects = new Dictionary<string, string>(),
            };
        }

        log.LogDebug("Reusing target folder {TargetFolder}", targetFolder);
        log.LogDebug("Reading persistent state cache…");
        return new CacheStore().Read();
    }

}
