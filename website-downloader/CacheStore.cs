namespace kcode.website_downloader;

internal sealed class CacheStore
{
    public string BaseDir { get; }

    public CacheStore(string baseDirPath = "cache")
    {
        BaseDir = baseDirPath;
    }

    public WebsiteData Read() => new()
    {
        FoundUrls = DeserializeFile<HashSet<string>>(CacheDataType.KnownUrl),
        LocalUrlPaths = DeserializeFile<HashSet<string>>(CacheDataType.KnownPath),
        FilteredLocalUrlPaths = DeserializeFile<HashSet<string>>(CacheDataType.FilteredKnownPath),
        HandledLocalSubpaths = DeserializeFile<HashSet<string>>(CacheDataType.HandledPath),
        WrittenFiles = DeserializeFile<HashSet<string>>(CacheDataType.WrittenFiles),
        Redirects = DeserializeFile<Dictionary<string, string>>(CacheDataType.KnownRedirect),
    };

    private T DeserializeFile<T>(CacheDataType type) where T : class, new()
    {
        var filepath = GetFilepath(type);
        if (!File.Exists(filepath))
        {
            var target = new T();
            return target;
        }

        using var stream = OpenRead(filepath);
        return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidOperationException("Invalid cache file content");
    }

    public void Write(WebsiteData data)
    {
        Write(CacheDataType.KnownUrl, data.FoundUrls);
        Write(CacheDataType.KnownPath, data.LocalUrlPaths);
        Write(CacheDataType.FilteredKnownPath, data.FilteredLocalUrlPaths);
        Write(CacheDataType.HandledPath, data.HandledLocalSubpaths);
        Write(CacheDataType.WrittenFiles, data.WrittenFiles);
        Write(CacheDataType.KnownRedirect, data.Redirects);
    }

    public void Write<T>(CacheDataType type, T list) where T : class
    {
        var filename = GetFilepath(type);
        Directory.CreateDirectory(BaseDir);
        new FileInfo(filename).Directory?.Create();
        using var stream = OpenWrite(filename);
        JsonSerializer.Serialize(stream, list);
    }

    private string GetFilepath(CacheDataType type)
    {
        var typeName = Enum.GetName(type) ?? throw new ArgumentException("Invalid enum value", paramName: nameof(type));
        var fname = typeName + ".json";
        return Path.Combine(BaseDir, fname);
    }

    private static Stream OpenRead(string filepath) => Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
    private static Stream OpenWrite(string filepath) => Open(filepath, FileMode.Create, FileAccess.Write, FileShare.None);
    private static Stream Open(string filepath, FileMode fm, FileAccess fa, FileShare fs) => new FileStream(filepath, fm, fa, fs);
}
