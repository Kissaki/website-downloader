# Website Downloader

Given a set of hostnames, starting from the index page, Website Downloader will crawl through every webpage examining every link on them and saving them as local files. Links to external websites (with other hostnames) are not examined.

If you have the .NET Core SDK 3.0 (preview) installed you can run the project from the `website-downloader` folder with the `dotnet run -- --hostnames example.org,www.example.org``.

Otherwise, with a compiled binary, use `website-downloader --hostnames example.org,www.example.org`.

This will start the crawling process on `https://example.org/` (the first of the hostnames), and visit links to both hostnames in the crawling process.

Use `--help` to list all program arguments you can pass:

```
Usage:
Flags:
  --target-folder <folder-path>
  --reuse-target-folder
  --delete-target-folder
  --hostnames <hostname1,hostname2>
    e.g. --hostnames "example.org,www.example.org,example.com"
  --request-protocol [http|https]
  --quiet");
```

This is a .NET Core 3.0 project. .NET Core 3 is still in preview, but publicly available as such. You can install the .NET Core 3.0 preview.

As it is a .NET Core 3.0 console app project it is *cross-platform*, with an installed runtime or from a self-contained package that includes the .NET Core runtime.

To build the project use the `dotnet` command line program or open the solution with Visual Studio 2019 and the .NET Core 3.0 SDK installed, open the solution and build it.

## `CrawlingDownloader` settings for use-case specific implementation

At the moment the class `CrawlingDownloader` still has some specifics for handling Wordpress non-pretty page URLs and ignoring two URLs. These can be moved out as program arguments or a separate configuration. They could also still serve as a helper if you need to do adjustments as well.

The `QueryMappedKeys` values are used to map `/?cat=123` URLs to `cat/123.html` files for example.

## Redirecting URLs

When the webserver responds with redirects in the HTTP responses there is no differentiation between server configured redirects without content, or redirects where the page still serves valid HTML content.

At the moment known redirecting URLs are stored in the `Redirects` structure. So if you need to do something with them, or inspect them, you can use that.

## Reusing the target folder and caching for repeated partial runs

With the `--reuse-target-folder` flag passed the program can be run repeatedly, and it will reuse already downloaded webpage files instead of requesting them from the webserver again.

Furthermore, a cache is used to store program state/progress and knowledge metadata. This is implemented in the `Cache` class.

In the working directory a `cache` folder is created and cache files are stored with binary serialization of the known URLs, checked URLs, written files structures.

When the target folder is reused these cache files are also used to restore these structures across program starts. When the target folder does not exist or is being deleted (`--delete-target-folder`) then no state data is loaded from the cache.

## Progress

The crawling and downloading process is done in loops. It starts with the index page on the first hostname.

Then, in each loop, every known local URL is visited, the content downloaded and saved, and if it is a `text/html` webpage it is examined for `href` links. (This could be easily extended for other elements for example to include scripts, css and image files.)

For every loop iteration a progress bar is displayed with `.....` representing placeholders, and `xx...` and `xxxxx` representing the filling of the progress bar. The progress is against the number of known local URLs which have not been examined yet, and proceeds when they are being examined.

## Rate limiting

At the moment there is no rate limiting. So be aware of any spam/request burst protection, monitoring and banning you may have implemented on your website.

The actions are synchronous, so the request rate is not very high overall. But given that you download a lot of pages/files depending on your website size, the overall count can be high.
