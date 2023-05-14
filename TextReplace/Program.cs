using System.Diagnostics;
using TextReplace;

using var lFac = LoggerFactory.Create(ConfigureLogging);
var log = lFac.CreateLogger(typeof(Program));

if (!TryParseArgs(args, log, out var baseDir)) return ReturnCode.Error;

Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

//FixupCss(baseDir, log, lFac);

FixupHtml(baseDir, log, lFac);
//HtmlReplacer.FixupContent(@"C:\dev\website-downloader\data\mf-data\index.html", TextReplacer.s_utf8Encoding, null!);

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

static bool TryParseArgs(string[] args, ILogger log, [NotNullWhen(returnValue: true)] out DirectoryInfo? baseDir)
{
    using var lScope = log.BeginScope(nameof(TryParseArgs));

    if (args.Length != 1)
    {
        log.LogError("Missing call argument dirPath");
        baseDir = null;
        return false;
    }
    var dirPath = args[0];
    baseDir = new DirectoryInfo(dirPath);
    if (!baseDir.Exists)
    {
        log.LogError("Directory does not exist at {dirPath}", dirPath);
        return false;
    }

    return true;
}

static void FixupHtml(DirectoryInfo baseDir, ILogger log, ILoggerFactory lf)
{
    ICollection<FileInfo> htmlFiles = FileFinder.FindFilesRecursive(baseDir, "*.html");
    log.LogInformation("Identified {Count} html files", htmlFiles.Count);

    HtmlReplacer.FixupContent(htmlFiles, TextReplacer.s_utf8Encoding, lf);
}

static void FixupCss(DirectoryInfo baseDir, ILogger log, ILoggerFactory lf)
{
    ICollection<FileInfo> cssFiles = FileFinder.FindFilesRecursive(baseDir, "*.css");
    log.LogInformation("Identified {Count} css files", cssFiles.Count);

    CssReplacer.FixupContent(cssFiles, TextReplacer.s_utf8Encoding, lf);
}
