namespace kcode.website_downloader;

internal static class Program
{
    static int Main()
    {
        Console.Title = "Website Downloader";
        if (!ProgramArguments.Parse(out var args))
        {
            return 1;
        }
        CrawlingDownloader.Start(args);
        return 0;
    }
}
