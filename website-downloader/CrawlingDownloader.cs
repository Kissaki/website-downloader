using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace kcode.website_downloader;

internal sealed class CrawlingDownloader : IDisposable
{
    private readonly string RequestProtocol;
    private string[] HostNames { get; }
    private string TargetFolder { get; }
    private bool ReuseTargetFolder { get; }
    private bool DeleteTargetFolderBeforeUse { get; }
    private bool Quiet { get; }
    /// <summary>Verify only - no downloading</summary>
    private bool OnlyVerifyDownloaded { get; }

    private readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    /// <summary>
    /// For urls without a filename these query parameter keys will be mapped to filenames of the query key.
    /// </summary>
    //private readonly string[] QueryMappedKeys = new string[] { "page_id", "page", "cat", "feed", "author", "p", "post", "tag", "paged", "do", };
    private readonly string[] QueryMappedKeys = new string[] { "do", };
    private readonly string[] ContentTypesToParse = new string[] { "text/html", };
    private readonly string[] ParseFileExtensionsForHrefs = new string[] { ".html", };
    private readonly string[] IgnoredSubpathsPrefixes = new string[]
    {
        // Invision
        "/calendar/",
        "/online/",
        // Activity
        "/discover/",
        "/search/",
        "/startTopic/",
        "/login/",
        //"/profile/",
    };

    private readonly Regex[] IgnoredPaths = new Regex[]
    {
        new Regex("^/profile/[0-9]+-[a-zA-Z0-9]/content/.*"),
    };

    private bool IsDataChanged;

    private readonly HttpClient HttpClient;
    private readonly ILoggerFactory _lFac;
    private readonly ILogger _log;

    private WebsiteData _data;
    private readonly HashSet<string> VerifiedMissing = new();
    private readonly HashSet<string> VerifiedMismatch = new();

    public CrawlingDownloader(ProgramArguments args, WebsiteData data, ILoggerFactory lFac)
    {
        _lFac = lFac;
        _log = lFac.CreateLogger<CrawlingDownloader>();
        _data = data;

        TargetFolder = args.TargetFolder;
        ReuseTargetFolder = args.ReuseTargetFolder;
        DeleteTargetFolderBeforeUse = args.DeleteTargetFolderBeforeUse;
        HostNames = args.Hostnames;
        RequestProtocol = args.RequestProtocol;
        Quiet = args.Quiet;
        OnlyVerifyDownloaded = args.VerifyDownloaded;

        var httpClientHandler = new HttpClientHandler { AllowAutoRedirect = false, };
        HttpClient = new HttpClient(httpClientHandler, disposeHandler: true);
    }

    public void StartCrawling()
    {
        if (DeleteTargetFolderBeforeUse)
        {
            DeleteTargetFolderBeforeUseImpl(out bool cancel);
            if (cancel) return;
        }

        if (ReuseTargetFolder)
        {
            UpdateStateWrittenFiles();
            //ReadUrlsFromAlreadyWrittenFiles();
        }
        WriteCache();

        _log.LogDebug("State:");
        _log.LogDebug("{state}", _data.GetStateDescription());

        // Starting point
        HandleSubpath("/");

        HashSet<string> unhandled;
        while ((unhandled = GetUnhandledLocalSubpaths()) != null && unhandled.Count != 0)
        {
            _log.LogInformation("Starting another round of checking known unchecked site page URLs ({unhandledCount})…", unhandled.Count);

            _log.LogDebug("State:");
            _log.LogDebug("{state}", _data.GetStateDescription());

            var placeCount = Math.Min(unhandled.Count, Math.Min(Console.BufferWidth - 12, 100));
            var i = 0;
            // Division with integer ceiling rounding to make sure we progress slower instead of faster (and bleed out)
            var factor = (unhandled.Count + placeCount - 1) / placeCount;
            Console.Write(new string('.', placeCount) + "\r");
            // As we modify the list while iterating through it, we make a copy for iteration
            foreach (var subpath in unhandled)
            {
                try
                {
                    HandleSubpath(subpath);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("WARN: {exMessage}", ex.Message);
                }
                ++i;
                if (i % factor == 0)
                {
                    Console.Write('x');
                }
            }
            Console.Write(new string('x', placeCount - i / factor));
            Console.WriteLine();
            WriteCache();
        }

        if (OnlyVerifyDownloaded)
        {
            _log.LogInformation("Done checking downloaded files. Missing {VerifiedMismatchCount}, mismates {VerifiedMismatchCount}.", VerifiedMismatch.Count, VerifiedMismatch.Count);
            var pathMissing = new FileInfo("missing.txt");
            var pathMismatch = new FileInfo("mismatch.txt");
            File.WriteAllLines(pathMissing.FullName, VerifiedMissing);
            File.WriteAllLines(pathMismatch.FullName, VerifiedMismatch);
            _log.LogInformation("The filepaths of these files with issues have been saved to {pathMissingFullName} and {pathMismatch}.", pathMissing.FullName, pathMismatch);
        }

        _log.LogInformation("All done! Check the target folder for the results at {TargetFolder}", TargetFolder);
    }

    private void DeleteTargetFolderBeforeUseImpl(out bool cancel)
    {
        if (Directory.Exists(TargetFolder))
        {
            if (!Quiet)
            {
                var choice = AskUser(question: "Are you sure you want to delete the target folder before starting? [Y/n]", acceptedInput: new string[] { "y", "n", });
                if (choice == "n")
                {
                    cancel = true;
                    return;
                }
            }

            _log.LogDebug("Removing existing target folder {TargetFolder}…", TargetFolder);
            Directory.Delete(TargetFolder, recursive: true);
        }
        cancel = false;
    }

    private static string AskUser(string question, string[] acceptedInput)
    {
        string line;
        do
        {
            Console.WriteLine(question);
            line = Console.ReadLine()?.Trim().ToLower() ?? "";
        } while (!acceptedInput.Contains(line));
        return line;
    }

    private static Stream OpenRead(string filepath) => Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
    private static Stream OpenWrite(string filepath) => Open(filepath, FileMode.Create, FileAccess.Write, FileShare.None);
    private static Stream Open(string filepath, FileMode fm, FileAccess fa, FileShare fs) => new FileStream(filepath, fm, fa, fs);

    private HashSet<string> GetUnhandledLocalSubpaths() => _data.LocalUrlPaths.Except(_data.HandledLocalSubpaths).ToHashSet();

    private void WriteCache()
    {
        if (!IsDataChanged) return;

        _log.LogDebug("Writing persistent state cache…");
        new CacheStore().Write(_data);
        IsDataChanged = false;
    }

    private void UpdateStateWrittenFiles()
    {
        // Verify only - no downloading
        if (OnlyVerifyDownloaded) return;

        _log.LogDebug("Reading already written webpage files…");
        UpdateStateWrittenFiles(new DirectoryInfo(TargetFolder));
    }

    /// <summary>Determines already written files and fills <see cref="WrittenFiles"/></summary>
    private void UpdateStateWrittenFiles(DirectoryInfo folder)
    {
        var actual = folder.GetFiles("*", SearchOption.AllDirectories).Select(x => x.FullName);

        var missing = _data.WrittenFiles.Except(actual);
        foreach (var fpath in missing)
        {
            _log.LogDebug("Written file is missing at {Filepath} - discarding", fpath);
        }

        var additional = actual.Except(_data.WrittenFiles);
        foreach (var fpath in additional)
        {
            _log.LogDebug("Written file is not known at {Filepath} - adding", fpath);
        }

        if (!actual.SequenceEqual(additional))
        {
            _data.WrittenFiles = actual.ToHashSet();
            IsDataChanged = true;
        }
    }

    private void ReadUrlsFromAlreadyWrittenFiles()
    {
        var filesToParse = _data.WrittenFiles.Where(filepath => ParseFileExtensionsForHrefs.Contains(new FileInfo(filepath).Extension)).ToArray();
        foreach (var filepath in filesToParse)
        {
            var content = File.ReadAllText(filepath, Encoding);
            var urls = UrlFinder.FindUrls(content);
            foreach (var url in urls) HandleNewUrl(url);
        }
    }

    private void HandleNewUrl(string url)
    {
        _data.FoundUrls.Add(url);
        IsDataChanged = true;

        if (url.Length == 0) return;
        if (url.StartsWith("#")) return;

        ExtractSubpath(url);
    }

    private void ExtractSubpath(string url)
    {
        var r = new Regex(@"^(?:(?<protocol>[a-zA-Z0-9]+)\:)?(?:\/\/)?(?<host>[^\/]+)?(?:\:(?<port>[0-9]+))?(?<subpath>.*)$");
        var match = r.Match(url);
        if (!match.Success) throw new NotImplementedException($"Unexpected url format could not be understood: {url}");

        var host = match.Groups["host"].Value;
        // A link without a host is a relative link. As we only visit content from our host under test the relative links are always links to the host under test.
        var isLocal = host.Length == 0 || HostNames.Contains(host);
        if (!isLocal) return;

        _data.LocalUrlPaths.Add(match.Groups["subpath"].Value);
        IsDataChanged = true;
    }

    private void HandleSubpath(string subpath)
    {
        if (subpath.Length == 0)
        {
            _data.HandledLocalSubpaths.Add(subpath);
            IsDataChanged = true;
            _log.LogWarning("Ignoring empty subpath {subpath}", subpath);
            return;
        }

        if (!subpath.StartsWith("/")) throw new ArgumentException($"subpath must be absolute (start with a slash '/')");

        if (_data.HandledLocalSubpaths.Contains(subpath))
        {
            _log.LogDebug("Ignoring already handled subpath {subpath}", subpath);
            return;
        }

        var ignoredPrefix = IgnoredSubpathsPrefixes.FirstOrDefault(ignoredPrefix => subpath.StartsWith(ignoredPrefix));
        if (ignoredPrefix != null)
        {
            _log.LogDebug("Ignoring subpath with ignored prefix; {ignoredPrefix} in {subpath}", ignoredPrefix, subpath);
            _data.HandledLocalSubpaths.Add(subpath);
            IsDataChanged = true;
            return;
        }

        foreach (var filter in IgnoredPaths)
        {
            if (filter.Match(subpath).Success)
            {
                _log.LogDebug("Ignoring subpath {Subpath} that matches an ignore-path-regex", subpath);
                _data.HandledLocalSubpaths.Add(subpath);
                IsDataChanged = true;
                return;
            }
        }

        _log.LogDebug("Checking {subpath}…", subpath);

        _data.HandledLocalSubpaths.Add(subpath);
        IsDataChanged = true;

        var filepath = GetFilepathFor(subpath);

        if (_data.WrittenFiles.Contains(filepath))
        {
            if (!_data.HandledLocalSubpaths.Contains(subpath))
            {
                var existingContent = File.ReadAllText(filepath, Encoding);
                var foundUrls = UrlFinder.FindUrls(existingContent);
                foreach (var foundUrl in foundUrls) HandleNewUrl(foundUrl);
            }
            _log.LogWarning("Trying to write this file a second time. Probably a URL mapping to filepath that is ambiguous. For {filepath}", filepath);
            //throw new InvalidOperationException($"Trying to write this file a second time. Probably a URL mapping to filepath that is ambiguous. For {filepath}");
            return;
        }

        var url = $"{RequestProtocol}://{HostNames[0]}{subpath}";
        if (!TryRequestWeb(url, out var resp)) return;

        // Recognize Location header URL even if the status code is not a redirect - https://developer.mozilla.org/en-US/docs/web/http/headers/location
        if (resp.Headers.Location != null) HandleNewUrl(resp.Headers.Location.AbsoluteUri);

        var isRedirect = IsRedirect(resp.StatusCode);
        if (isRedirect)
        {
            if (resp.Headers.Location == null)
            {
                _log.LogWarning("WARN: Redirect without Location header in response for {url}", url);
                throw new NotImplementedException("Redirect without Location header in response for ");
            }
            else
            {
                _log.LogDebug("Identified as redirect {subpath}", subpath);
                // Redirect pages can have content as well.
                // Or they can be webserver redirects.
                // For now cache but otherwise ignore them (do not write them; this could lead to a /fragment -> /fragment/ redirect to try and produce a fragment directory and file)
                _data.Redirects.Add(subpath, resp.Headers.Location!.AbsoluteUri);
                IsDataChanged = true;
                return;
            }
        }

        var content = resp.Content;
        var isHtml = content.Headers.ContentType != null && ContentTypesToParse.Contains(content.Headers.ContentType.MediaType);
        if (isHtml)
        {
            var text = content.ReadAsStringAsync().Result;
            WriteHtmlFile(filepath, text);
            var foundUrls = UrlFinder.FindUrls(text);
            foreach (var foundUrl in foundUrls) HandleNewUrl(foundUrl);
        }
        else
        {
            using var binaryStream = content.ReadAsStreamAsync().Result;
            WriteBinaryFile(filepath, binaryStream);
        }

        bool TryRequestWeb(string url, [NotNullWhen(returnValue: true)] out HttpResponseMessage? resp)
        {
            _log.LogDebug("Downloading {url}…", url);
            try
            {
                resp = HttpClient.GetAsync(url).Result;
                return true;
            }
            catch (WebException e)
            {
                _log.LogWarning("Fetch failed of {url} with exception {eMessage}", url, e.Message);
                resp = null;
                return false;
            }
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => (int)statusCode is >= 300 and <= 399;

    private void WriteHtmlFile(string filepath, string content)
    {
        // File was already written
        if (_data.WrittenFiles.Contains(filepath)) return;

        if (OnlyVerifyDownloaded)
        {
            if (!File.Exists(filepath))
            {
                VerifiedMissing.Add(filepath);
                return;
            }
            using var reader = File.OpenRead(filepath);
            if (content.Length != reader.Length)
            {
                VerifiedMismatch.Add(filepath);
                return;
            }
        }

        _log.LogDebug("Writing file {filepath}…", filepath);
        new FileInfo(filepath).Directory!.Create();
        File.WriteAllText(filepath, content, Encoding);
        _data.WrittenFiles.Add(filepath);
        IsDataChanged = true;
    }

    private void WriteBinaryFile(string filepath, Stream binaryStream)
    {
        if (_data.WrittenFiles.Contains(filepath))
        {
            return;
        }

        if (OnlyVerifyDownloaded)
        {
            if (!File.Exists(filepath))
            {
                VerifiedMissing.Add(filepath);
                return;
            }
            using var reader = File.OpenRead(filepath);
            if (binaryStream.Length != reader.Length)
            {
                VerifiedMismatch.Add(filepath);
                return;
            }
        }

        _log.LogDebug("Writing file {filepath}…", filepath);
        new FileInfo(filepath).Directory!.Create();
        using var writer = File.OpenWrite(filepath);
        binaryStream.CopyTo(writer);
    }

    private string GetFilepathFor(string subpath)
    {
        var segments = subpath.Split("/");
        var filename = segments.Last();
        segments = segments.SkipLast(1).ToArray();

        var queryIndex = filename.IndexOf("?");
        var query = string.Empty;
        if (queryIndex != -1)
        {
            query = filename[(queryIndex + 1)..];
            filename = filename[..queryIndex];
        }

        char? last = null;
        for (var i = 0; i < query.Length; ++i)
        {
            var c = query[i];
            if (c == '#' && (last == null || last != '&'))
            {
                query = query[..i];
            }
            last = c;
        }

        var queryElements = query.Split("&");
        var queryParameters = queryElements.Select(x => {
            var parts = x.Split("=");
            return new
            {
                Key = parts[0],
                Value = parts.Length > 1 ? parts[1] : null,
            };
        }).Where(x => x.Key.Length > 0);

        var combined = Path.Combine(segments);

        if (filename.Length == 0)
        {
            if (!queryParameters.Any())
            {
                filename = "index.html";
            }
            else
            {
                var qp = queryParameters.SingleOrDefault(x => QueryMappedKeys.Contains(x.Key));
                if (qp != null)
                {
                    combined = Path.Combine(combined, qp.Key);
                    filename = $"{qp.Value}.html";
                }
                else
                {
                    //_log.LogWarning("Ignoring url with query parameter {subpath}", subpath);
                    throw new NotImplementedException($"No filename and unhandled query filename mapping for subpath {subpath}");
                }
            }
        }
        combined = Path.Combine(combined, filename);

        if (combined.Length == 0)
        {
            throw new InvalidOperationException();
        }
        var filepath = Path.Combine(TargetFolder, combined);
        return filepath;
    }

    public void Dispose()
    {
        HttpClient .Dispose();
    }
}
