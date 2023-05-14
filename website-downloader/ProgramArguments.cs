namespace kcode.website_downloader;

internal class ProgramArguments
{
    public static bool Parse(ILoggerFactory lFac, out ProgramArguments progArgs)
    {
        var log = lFac.CreateLogger<ProgramArguments>();

        progArgs = new ProgramArguments(lFac);

        var cliArgs = Environment.GetCommandLineArgs();
        for (var i = 1; i < cliArgs.Length; ++i)
        {
            var arg = cliArgs[i];
            if (arg.StartsWith("--"))
            {
                var key = arg["--".Length..];
                switch (key)
                {
                    case "help":
                        PrintUsage();
                        return false;
                    case "target-folder":
                        var value = cliArgs[++i];
                        progArgs.TargetFolder = value;
                        break;
                    case "reuse-target-folder":
                        progArgs.ReuseTargetFolder = true;
                        break;
                    case "delete-target-folder":
                        progArgs.DeleteTargetFolderBeforeUse = true;
                        break;
                    case "hostnames":
                        var values = cliArgs[++i];
                        progArgs.Hostnames = values.Split(",").Select(x => x.Trim()).ToArray();
                        break;
                    case "request-protocol":
                        var prot = cliArgs[++i];
                        progArgs.RequestProtocol = prot;
                        break;
                    case "quiet":
                        progArgs.Quiet = true;
                        break;
                    case "verify-downloaded":
                        progArgs.VerifyDownloaded = true;
                        break;
                    default:
                        Console.Error.WriteLine($"Invalid command line argument {arg}");
                        PrintUsage();
                        return false;
                }
            }
            else
            {
                log.LogError("Invalid command line argument {arg}", arg);
                return false;
            }
        }

        return progArgs.Validate();
    }

    public static void PrintUsage()
    {
        Console.Write("""
            Usage:
            Flags:
              --target-folder <folder-path>
              --reuse-target-folder
              --delete-target-folder
              --hostnames <hostname1,hostname2>
                e.g. --hostnames "example.org,www.example.org,example.com"
              --request-protocol [http|https]
              --quiet
            """);
    }

    public string TargetFolder { get; set; } = "downloaded";
    public bool ReuseTargetFolder { get; set; }
    public bool DeleteTargetFolderBeforeUse { get; set; }
    public string[] Hostnames { get; set; }
    public string RequestProtocol { get; set; } = "https";
    public bool Quiet { get; set; }
    public bool VerifyDownloaded { get; set; }

    private readonly ILogger<ProgramArguments> _log;

    public ProgramArguments(ILoggerFactory lFac)
    {
        _log = lFac.CreateLogger<ProgramArguments>();
    }

    public bool Validate()
    {
        if (Hostnames.Length == 0)
        {
            _log.LogError("Missing hostnames");
            PrintUsage();
            return false;
        }
        if (Directory.Exists(TargetFolder) && !ReuseTargetFolder && !DeleteTargetFolderBeforeUse)
        {
            _log.LogError("The specified target directory already exists and neither --reuse-target-folder nor --delete-target-folder was specified. --target-folder '{TargetFolder}'.", TargetFolder);
            PrintUsage();
            return false;
        }

        return true;
    }
}
