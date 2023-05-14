namespace TextReplace;

internal static class CssReplacer
{
    /// <param name="fileEncoding">For null defaults to utf8</param>
    public static void FixupContent(ICollection<FileInfo> filePaths, Encoding fileEncoding, ILoggerFactory lf)
    {
        var log = lf.CreateLogger(typeof(CssReplacer));

        log.LogInformation("Staring css content fixup ({Count} files)…", filePaths.Count);
        var i = 0;
        foreach (var filePath in filePaths.Select(x => x.FullName))
        {
            if (++i % 100 == 0) log.LogInformation("{i}/{Count}", i, filePaths.Count);

            var text = File.ReadAllText(filePath, fileEncoding);

            ReplaceMumbleFontawesomeUrl(ref text);

            File.WriteAllText(filePath, text, fileEncoding);
        }
        log.LogInformation("Finished replace");
    }

    /// <summary>Replace absolute URL with relative path</summary>
    public static void ReplaceMumbleFontawesomeUrl(ref string text) => TextReplacer.ReplaceText(ref text, @"//forums.mumble.info/applications/core/interface/font/fontawesome-webfont.woff", "../../applications/core/interface/font/fontawesome-webfont.woff");
}
