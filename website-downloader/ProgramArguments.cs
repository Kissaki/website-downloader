namespace kcode.website_downloader;

class ProgramArguments
{
    public static bool Parse(out ProgramArguments progArgs)
    {
        progArgs = new ProgramArguments();

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
                Console.Error.WriteLine($"Invalid command line argument {arg}");
                return false;
            }
        }

        return progArgs.Validate();
    }

    public static void PrintUsage()
    {
        Console.WriteLine($"Usage:");
        Console.WriteLine("Flags:");
        Console.WriteLine("  --target-folder <folder-path>");
        Console.WriteLine("  --reuse-target-folder");
        Console.WriteLine("  --delete-target-folder");
        Console.WriteLine(@"  --hostnames <hostname1,hostname2>");
        Console.WriteLine(@"    e.g. --hostnames ""example.org,www.example.org,example.com""");
        Console.WriteLine("  --request-protocol [http|https]");
        Console.WriteLine("  --quiet");
    }

    public string TargetFolder { get; set; } = "downloaded";
    public bool ReuseTargetFolder { get; set; }
    public bool DeleteTargetFolderBeforeUse { get; set; }
    public string[] Hostnames { get; set; } = Array.Empty<string>();
    public string RequestProtocol { get; set; } = "https";
    public bool Quiet { get; set; }
    public bool VerifyDownloaded { get; set; }

    public ProgramArguments()
    {
    }

    public bool Validate()
    {
        if (Hostnames.Length == 0)
        {
            Error($"No hostnames specified");
            return false;
        }
        if (Directory.Exists(TargetFolder) && !ReuseTargetFolder && !DeleteTargetFolderBeforeUse)
        {
            Error($"The specified target directory already exists and neither --reuse-target-folder nor --delete-target-folder was specified. --target-folder '{TargetFolder}'.");
            return false;
        }

        return true;
    }

    private static void Error(string err)
    {
        Console.Error.WriteLine(err);
        PrintUsage();
    }
}
