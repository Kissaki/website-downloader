namespace kcode.website_downloader;

internal record WebsiteData
{
    /// <summary>Discovered links - full form with protocol and hostname</summary>
    public required HashSet<string> FoundUrls { get; init; }
    /// <summary>Discovered subpaths (cleaned up URLs - URL paths + +</summary>
    public required HashSet<string> LocalUrlPaths { get; init; }
    public required HashSet<string> FilteredLocalUrlPaths { get; init; }
    public required HashSet<string> HandledLocalSubpaths { get; init; }
    public required HashSet<string> WrittenFiles { get; set; }
    public required Dictionary<string, string> Redirects { get; init; }

    public string GetStateDescription() => $"""
            Found URLs: {FoundUrls.Count}
            Normalized paths: {LocalUrlPaths.Count}
            Filtered normalized paths: {FilteredLocalUrlPaths.Count}
            Handled paths: {HandledLocalSubpaths.Count}
            Written files: {WrittenFiles.Count}
            Found Redirects: {Redirects.Count}
        """;
}
