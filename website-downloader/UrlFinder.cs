using System.Text.RegularExpressions;

namespace kcode.website_downloader;

internal static class UrlFinder
{
    private static readonly Regex rHref = new(@"((href)|(src))=[""'](?<url>[^""']+)[""']");

    public static string[] FindUrls(string content)
    {
        var hrefs = rHref.Matches(content);
        return hrefs.Cast<Match>().Select(href => href.Groups["url"].Value).ToArray();
    }
}
