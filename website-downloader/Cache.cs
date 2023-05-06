namespace kcode.website_downloader;

internal sealed class Cache
{
    public enum Type
    {
        KnownGlobal,
        KnownLocal,
        Handled,
        WrittenFiles,
        Redirects,
    }

    private const string BaseDir = "cache";

    public void Read<T>(Type type, out T target) where T : class, new()
    {
        var filepath = GetFilepath(type);
        if (!File.Exists(filepath))
        {
            target =  new T();
            return;
        }

        using var stream = OpenRead(filepath);
        target = JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidOperationException("Invalid cache file content");
    }

    public void Write<T>(Type type, T list) where T : class
    {
        var filename = GetFilepath(type);
        Directory.CreateDirectory(BaseDir);
        new FileInfo(filename).Directory?.Create();
        using var stream = OpenWrite(filename);
        JsonSerializer.Serialize(stream, list);
    }

    private static string GetFilepath(Type type) => Path.Combine(BaseDir, Enum.GetName(type) ?? throw new ArgumentException("Invalid enum value", paramName: nameof(type)));
    private static Stream OpenRead(string filepath) => Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
    private static Stream OpenWrite(string filepath) => Open(filepath, FileMode.Create, FileAccess.Write, FileShare.None);
    private static Stream Open(string filepath, FileMode fm, FileAccess fa, FileShare fs) => new FileStream(filepath, fm, fa, fs);
}
