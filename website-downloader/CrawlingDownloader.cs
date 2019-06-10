using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace kcode.website_downloader
{
    class CrawlingDownloader : IDisposable
    {
        public static void Start(ProgramArguments args) => (new CrawlingDownloader(args)).StartCrawling();

        private string RequestProtocol;
        private string[] HostNames { get; }
        private string TargetFolder { get; }
        private bool ReuseTargetFolder { get; }
        private bool DeleteTargetFolderBeforeUse { get; }
        private bool Quiet { get; }
        private bool VerifyDownloaded { get; }

        private Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        /// <summary>
        /// For urls without a filename these query parameter keys will be mapped to filenames of the query key.
        /// </summary>
        private string[] QueryMappedKeys = new string[] { "page_id", "page", "cat", "feed", "author", "p", "post", "tag", "paged", };
        private string[] ContentTypesToParse = new string[] { "text/html", };
        private List<string> IgnoredSubpaths = new List<string>
        {
            // Returns the standard homepage
            "/community-blogs/",
            // Returns the standard homepage; should be redirect to "/?page_id=578" (podcasts)
            "/?option=com_podcast&amp;view=feed&amp;format=raw",
        };

        private bool IsDataChanged;

        private HttpClientHandler HttpClientHandler;
        private HttpClient HttpClient;

        private HashSet<string> KnownGlobalLinks;
        private HashSet<string> KnownLocalSubpaths;
        private HashSet<string> HandledLocalSubpaths;
        private HashSet<string> WrittenFiles;
        public Dictionary<string, string> Redirects;
        private HashSet<string> VerifiedMissing = new HashSet<string>();
        private HashSet<string> VerifiedMismatch = new HashSet<string>();

        private CrawlingDownloader(ProgramArguments args)
        {
            TargetFolder = args.TargetFolder;
            ReuseTargetFolder = args.ReuseTargetFolder;
            DeleteTargetFolderBeforeUse = args.DeleteTargetFolderBeforeUse;
            HostNames = args.Hostnames;
            RequestProtocol = args.RequestProtocol;
            Quiet = args.Quiet;
            VerifyDownloaded = args.VerifyDownloaded;

            HttpClientHandler = new HttpClientHandler { AllowAutoRedirect = false, };
            HttpClient = new HttpClient(HttpClientHandler, disposeHandler: true);
        }

        private void StartCrawling()
        {
            if (DeleteTargetFolderBeforeUse)
            {
                if (Directory.Exists(TargetFolder))
                {
                    if (!Quiet)
                    {
                        string line;
                        do
                        {
                            Console.WriteLine($"Are you sure you want to delete the target folder before starting? [Y/n]");
                            line = Console.ReadLine();
                        } while (line.Length != 0 && line.ToLower() != "y" && line.ToLower() != "n");
                        if (line.ToLower() == "n")
                        {
                            return;
                        }
                    }

                    Debug.WriteLine($"Removing existing target folder {TargetFolder}…");
                    Directory.Delete(TargetFolder, recursive: true);
                }
            }
            if (ReuseTargetFolder && Directory.Exists(TargetFolder))
            {
                Debug.WriteLine($"Reusing target folder {TargetFolder}");
                ReadCache();
                ReadAlreadyWritten();
            }
            else
            {
                InitNoCache();
                Directory.CreateDirectory(TargetFolder);
            }

            HandleSubpath("/");

            HashSet<string> unhandled;
            while ((unhandled = GetUnhandledLocalSubpaths()) != null && unhandled.Count != 0)
            {
                Console.WriteLine($"Starting another round of checking known unchecked site page URLs ({unhandled.Count})…");
                var placeCount = Math.Min(unhandled.Count, Math.Min(Console.BufferWidth - 12, 100));
                var i = 0;
                // Division with integer ceiling rounding to make sure we progress slower instead of faster (and bleed out)
                var factor = (unhandled.Count + placeCount - 1) / placeCount;
                Console.Write(new string('.', placeCount) + "\r");
                // As we modify the list while iterating through it, we make a copy for iteration
                foreach (var subpath in unhandled)
                {
                    HandleSubpath(subpath);
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

            if (VerifyDownloaded)
            {
                Console.WriteLine($"Done checking downloaded files. Missing {VerifiedMismatch.Count}, mismates {VerifiedMismatch.Count}.");
                var pathMissing = new FileInfo("missing.txt");
                var pathMismatch = new FileInfo("mismatch.txt");
                File.WriteAllLines(pathMissing.FullName, VerifiedMissing);
                File.WriteAllLines(pathMismatch.FullName, VerifiedMismatch);
                Console.WriteLine($"The filepaths of these files with issues have been saved to {pathMissing.FullName} and {pathMismatch}.");
            }

            Console.WriteLine($"All done! Check the target folder for the results at {TargetFolder}");
        }
        private Stream OpenRead(string filepath) => Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
        private Stream OpenWrite(string filepath) => Open(filepath, FileMode.Create, FileAccess.Write, FileShare.None);
        private Stream Open(string filepath, FileMode fm, FileAccess fa, FileShare fs) => new FileStream(filepath, fm, fa, fs);

        private HashSet<string> GetUnhandledLocalSubpaths() => KnownLocalSubpaths.Except(HandledLocalSubpaths).ToHashSet();

        private void InitNoCache()
        {
            KnownGlobalLinks = new HashSet<string>();
            KnownLocalSubpaths = new HashSet<string>();
            HandledLocalSubpaths = new HashSet<string>();
            WrittenFiles = new HashSet<string>();
            Redirects = new Dictionary<string, string>();
        }

        private void ReadCache()
        {
            Debug.WriteLine("Reading persistent state cache…");
            var cache = new Cache();
            cache.Read(Cache.Type.KnownGlobal, out KnownGlobalLinks);
            cache.Read(Cache.Type.KnownLocal, out KnownLocalSubpaths);
            cache.Read(Cache.Type.Handled, out HandledLocalSubpaths);
            cache.Read(Cache.Type.WrittenFiles, out WrittenFiles);
            cache.Read(Cache.Type.Redirects, out Redirects);
            IsDataChanged = false;
        }

        private void WriteCache()
        {
            if (!IsDataChanged)
            {
                return;
            }

            Debug.WriteLine("Writing persistent state cache…");
            var cache = new Cache();
            cache.Write(Cache.Type.KnownGlobal, KnownGlobalLinks);
            cache.Write(Cache.Type.KnownLocal, KnownLocalSubpaths);
            cache.Write(Cache.Type.Handled, HandledLocalSubpaths);
            cache.Write(Cache.Type.WrittenFiles, WrittenFiles);
            cache.Write(Cache.Type.Redirects, Redirects);
            IsDataChanged = false;
        }

        private void ReadAlreadyWritten()
        {
            if (VerifyDownloaded)
            {
                // When we want to verify the downloaded files, we do no parse them.
                return;
            }

            Debug.WriteLine("Reading already written webpage files…");
            DetermineAlreadyWrittenFiles(new DirectoryInfo(TargetFolder));
            ReadAlreadyWrittenFiles();
        }

        private void DetermineAlreadyWrittenFiles(DirectoryInfo folder)
        {
            foreach (var fi in folder.GetFiles())
            {
                var filepath = fi.FullName;
                if (!WrittenFiles.Contains(filepath))
                {
                    WrittenFiles.Add(filepath);
                    IsDataChanged = true;
                }
            }
            foreach (var di in folder.GetDirectories())
            {
                DetermineAlreadyWrittenFiles(di);
            }
        }

        private void ReadAlreadyWrittenFiles()
        {
            var missing = new HashSet<string>();
            foreach (var filepath in WrittenFiles)
            {
                if (!File.Exists(filepath))
                {
                    missing.Add(filepath);
                    continue;
                }
                var content = File.ReadAllText(filepath, Encoding);
                HandleContent(content);
            }
            foreach (var filepath in missing)
            {
                Debug.WriteLine($"Previously written file is missing. Discarding knowledge about {filepath}");
                WrittenFiles.Remove(filepath);
                IsDataChanged = true;
            }
        }

        private void HandleContent(string content)
        {
            var rHref = new Regex(@"href=""(?<url>[^""]*)""");
            var hrefs = rHref.Matches(content);
            foreach (Match href in hrefs)
            {
                var url = href.Groups["url"].Value;
                HandleNewUrl(url);
            }
        }

        private void HandleNewUrl(string url)
        {
            KnownGlobalLinks.Add(url);
            IsDataChanged = true;

            if (url.Length == 0 || url.StartsWith("#"))
            {
                return;
            }

            var r = new Regex(@"^(?:(?<protocol>[a-zA-Z0-9]+)\:)?(?:\/\/)?(?<host>[^\/]+)?(?:\:(?<port>[0-9]+))?(?<subpath>.*)$");
            var match = r.Match(url);
            if (!match.Success)
            {
                throw new NotImplementedException($"Unexpected url format could not be understood: {url}");
            }
            var host = match.Groups["host"].Value;
            // A link without a host is a relative link. As we only visit content from our host under test the relative links are always links to the host under test.
            var isLocal = host.Length == 0 || HostNames.Contains(host);
            if (!isLocal)
            {
                return;
            }
            KnownLocalSubpaths.Add(match.Groups["subpath"].Value);
            IsDataChanged = true;
        }

        private void HandleSubpath(string subpath)
        {
            if (!subpath.StartsWith("/"))
            {
                throw new ArgumentException($"{nameof(subpath)} must be absolute (start with a slash '/')");
            }

            if (HandledLocalSubpaths.Contains(subpath))
            {
                return;
            }

            foreach (var ignoredPrefix in IgnoredSubpaths)
            {
                if (subpath.StartsWith(ignoredPrefix))
                {
                    HandledLocalSubpaths.Add(subpath);
                    IsDataChanged = true;
                    return;
                }
            }

            Debug.WriteLine($"Checking {subpath}…");

            HandledLocalSubpaths.Add(subpath);
            IsDataChanged = true;

            var filepath = GetFilepathFor(subpath);

            if (WrittenFiles.Contains(filepath))
            {
                if (!HandledLocalSubpaths.Contains(subpath))
                {
                    HandleContent(File.ReadAllText(filepath, Encoding));
                }
                return;
                //throw new InvalidOperationException($"Trying to write this file a second time. Probably a URL mapping to filepath that is ambiguous. For {filepath}");
            }

            var url = $"{RequestProtocol}://{HostNames[0]}{subpath}";
            Debug.WriteLine($"Downloading {url}…");
            HttpResponseMessage resp;
            try
            {
                resp = HttpClient.GetAsync(url).Result;
            }
            catch (WebException e)
            {
                Console.Error.WriteLine($"Ignoring {url} because it returned {e.Message}");
                return;
            }
            if (resp.Headers.Location != null)
            {
                HandleNewUrl(resp.Headers.Location.AbsoluteUri);
            }
            var isRedirect = IsRedirect(resp.StatusCode);
            if (isRedirect)
            {
                // Redirect pages can have content as well.
                // Or they can be webserver redirects.
                // For now cache but otherwise ignore them (do not write them; this could lead to a /fragment -> /fragment/ redirect to try and produce a fragment directory and file)
                Redirects.Add(subpath, resp.Headers.Location.AbsoluteUri);
                IsDataChanged = true;
                return;
            }
            var content = resp.Content;
            var isHtml = ContentTypesToParse.Contains(content.Headers.ContentType.MediaType);
            if (isHtml)
            {
                var text = content.ReadAsStringAsync().Result;
                WriteHtmlFile(filepath, text);
                HandleContent(text);
            }
            else
            {
                using var binaryStream = content.ReadAsStreamAsync().Result;
                WriteBinaryFile(filepath, binaryStream);
            }
        }

        private bool IsRedirect(HttpStatusCode statusCode) => (int)statusCode >= 300 && (int)statusCode <= 399;

        private void WriteHtmlFile(string filepath, string content)
        {
            if (WrittenFiles.Contains(filepath))
            {
                return;
            }

            if (VerifyDownloaded)
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

            Debug.WriteLine($"Writing file {filepath}…");
            (new FileInfo(filepath)).Directory.Create();
            File.WriteAllText(filepath, content, Encoding);
            WrittenFiles.Add(filepath);
            IsDataChanged = true;
        }

        private void WriteBinaryFile(string filepath, Stream binaryStream)
        {
            if (WrittenFiles.Contains(filepath))
            {
                return;
            }

            if (VerifyDownloaded)
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

            Debug.WriteLine($"Writing file {filepath}…");
            (new FileInfo(filepath)).Directory.Create();
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
                query = filename.Substring(queryIndex + 1);
                filename = filename.Substring(0, queryIndex);
            }

            char? last = null;
            for (var i = 0; i < query.Length; ++i)
            {
                var c = query[i];
                if (c == '#' && (last == null || last != '&'))
                {
                    query = query.Substring(0, i);
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
                        //filename = $"{qp.Key}-{qp.Value}.html";
                        combined = Path.Combine(combined, qp.Key);
                        filename = $"{qp.Value}.html";
                    }
                    else
                    {
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
}
