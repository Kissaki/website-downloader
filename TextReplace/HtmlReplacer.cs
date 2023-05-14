namespace TextReplace;

internal static class HtmlReplacer
{
    private static readonly ReplaceRegex[] s_regexReplaces = new ReplaceRegex[]
    {
    };

    private static readonly Replace[] s_textReplaces = new Replace[]
    {
        // Content cleanup
        new(@"'ipsNavBar_active'", ""),
        new(@"data-navDefault", ""),
        new(@" class='ipsNavBar_active' data-active ", ""),

        //new(@"https://fonts.googleapis.com/css?family=Inter:300,300i,400,400i,500,700,700i", @"uploads/css_built_1/fonts.googleapis.com_css_family-inter.css"),
        new(@"<link href=""https://fonts.googleapis.com/css?family=Inter:300,300i,400,400i,500,700,700i"" rel=""stylesheet"" referrerpolicy=""origin"">", ""),
    };

    public static void FixupContent(ICollection<FileInfo> filePaths, Encoding encoding, ILoggerFactory lf)
    {
        var log = lf.CreateLogger(typeof(HtmlReplacer));

        log.LogInformation("Staring replace...");
        var i = 0;
        foreach (var filePath in filePaths.Select(x => x.FullName))
        {
            if (++i % 100 == 0) log.LogInformation("{i}/{Count}", i, filePaths.Count);
            FixupContent(filePath, encoding, log);
        }
        log.LogInformation("Finished replace");
    }

    public static void FixupContent(string filepath, Encoding encoding, ILogger log)
    {
        var text = File.ReadAllText(filepath, encoding);
        var orig = text;

        //RemoveFormGeneratedCsrfAndRef(ref text);
        //RemoveLinkCsrfParams(ref text);
        //RemoveAllNavs(ref text);
        //FixupLocalLinks(ref text);
        //FixupContactUs(ref text);
        //RemoveThemeMenu(ref text);
        //RemoveLogin(ref text);
        //RemoveTopNav(ref text);
        // Set base - URLs have already been cleared to be base-relative
        //SetBaseHref(ref text);
        //RemoveTopicReplyComponent(ref text);
        //RemovePageNavJs(ref text);
        //AddArchiveHeader(ref text);
        //DisableJavascript(ref text);

        ReplaceBaseTmpToProd(ref text);
        //RemoveFooterStafflinkAgain(ref text);

        //ReplaceRegex(ref text);
        //ReplaceText(ref text);
        //ReplaceTextFromFiles(ref text);

        if (text != orig) File.WriteAllText(filepath, text, encoding);
    }

    private static void ReplaceBaseTmpToProd(ref string text)
    {
        TextReplacer.ReplaceText(ref text, @"<base href=""https://kcode.de/tmp/mumble-forums/"">", @"<base href=""https://forums.mumble.info/"">");
    }

    private static void DisableJavascript(ref string text)
    {
        TextReplacer.ReplaceRegex(ref text, new(@"(<script type='text/javascript'.+?</script>)", RegexOptions.Singleline), @"<!--$1-->");
    }

    private static void RemoveFooterStafflinkAgain(ref string text)
    {
        TextReplacer.ReplaceText(ref text, @"<li><a rel=""nofollow"" href='staff/' >Staff</a></li>", "");
    }

    private static void AddArchiveHeader(ref string text)
    {
        var inject = File.ReadAllText(@"C:\dev\website-downloader\TextReplace\rm-html\inject archive header.txt", TextReplacer.s_utf8Encoding);
        TextReplacer.ReplaceRegex(ref text, new(@"(<body[^>]+>)"), "$1\n" + inject);
    }

    private static void RemovePageNavJs(ref string text)
    {
        TextReplacer.ReplaceText(ref text, @"data-role=""tablePagination""", "");
    }

    private static void RemoveTopicReplyComponent(ref string text) => TextReplacer.ReplaceTextFromFile(ref text, @"C:\dev\website-downloader\TextReplace\rm-html\topic reply login.txt", TextReplacer.s_utf8Encoding, "");

    private static void SetBaseHref(ref string text)
    {
        //TextReplacer.ReplaceText(ref text, @"<head>", @"<head><base href=""file:///C:/dev/website-downloader/data/mf-data/"">");
        TextReplacer.ReplaceText(ref text, @"<head>", @"<head><base href=""https://kcode.de/tmp/mumble-forums/"">");
        //TextReplacer.ReplaceText(ref text, @"<head>", @"<head><base href=""https://forums.mumble.info/"">");
    }

    private static void RemoveTopNav(ref string text)
    {
        TextReplacer.ReplaceRegex(ref text, new(@"	<div id='ipsLayout_header' class='ipsClearfix'>.+?<main ", RegexOptions.Singleline), @"<main ");
        // All Activity /discover/ link
        TextReplacer.ReplaceRegex(ref text, new(@"	<ul class='ipsList_inline ipsPos_right'>.+?</ul>", RegexOptions.Singleline), @"");

        // old code
        //TextReplacer.ReplaceRegex(ref text, new(@"<nav data-controller='core.front.core.navBar' class=' ipsResponsive_showDesktop'>.+?<ul.+?</ul>.+?</ul>", RegexOptions.Singleline), "");
        //TextReplacer.ReplaceRegex(ref text, new(@"<ul id=[""]elMobileNav[""'].+?</ul>", RegexOptions.Singleline), "");
        //TextReplacer.ReplaceRegex(ref text, new(@"<ul class='ipsMobileHamburger .+?</ul>", RegexOptions.Singleline), "");
    }

    private static void RemoveLogin(ref string text)
    {
        var fpath = @"C:\dev\website-downloader\TextReplace\rm-html\nav userlogin.txt";
        TextReplacer.ReplaceTextFromFile(ref text, fpath, TextReplacer.s_utf8Encoding, "");
    }

    private static void RemoveThemeMenu(ref string text)
    {
        var fpath = @"C:\dev\website-downloader\TextReplace\rm-html\footer theme.txt";
        TextReplacer.ReplaceTextFromFile(ref text, fpath, TextReplacer.s_utf8Encoding, "");
    }

    /// <summary>Fixup Contact Us -> mumble.info/contact/</summary>
    private static void FixupContactUs(ref string text)
    {
        var original = @"<li><a rel=""nofollow"" href='contact/' >Contact Us</a></li>";
        var newFootl = @"<li><a rel=""nofollow"" href='https://www.mumble.info/contact/' >Contact Us</a></li>" + "\n" + @"<li><a rel=""nofollow"" href='staff/' >Staff</a></li>";
        TextReplacer.ReplaceText(ref text, original, newFootl);
    }

    private static void FixupLocalLinks(ref string text)
    {
        var replaces = new Replace[]
        {
            new(@"""https://forums.mumble.info""", ""),
            new(@"'https://forums.mumble.info'", ""),

            new(@"https://forums.mumble.info/", ""),
            new(@"//forums.mumble.info/", ""),
        };

        foreach (var replace in replaces) TextReplacer.ReplaceText(ref text, replace.oldValue, replace.newValue);
    }

    private static void RemoveFormGeneratedCsrfAndRef(ref string text)
    {
        // CSRF
        TextReplacer.ReplaceRegex(ref text, new(@"<input type=""hidden"" name=""csrfKey"" value=""[0-9a-z]+"">"), "");
        // Referrer
        TextReplacer.ReplaceRegex(ref text, new(@"<input type=""hidden"" name=""ref"" value=""[0-9a-zA-Z+/=]+"">"), "");

        // CSRF
        // First param with followup replaces with promoting second to first parameter
        TextReplacer.ReplaceRegex(ref text, new(@"\?csrfKey=[0-9a-z]+&"), "?");
        // First param without followup is a simple removal
        TextReplacer.ReplaceRegex(ref text, new(@"\?csrfKey=[0-9a-z]+[^&]"), "$1");
        // Followup param is a simple removal
        TextReplacer.ReplaceRegex(ref text, new(@"&csrfKey=[0-9a-z]+"), "");

        // Ref
        // First param with followup replaces with promoting second to first parameter
        TextReplacer.ReplaceRegex(ref text, new(@"\?ref=[0-9a-zA-Z+/=]+&"), "?");
        // First param without followup is a simple removal
        TextReplacer.ReplaceRegex(ref text, new(@"\?ref=[0-9a-zA-Z+/=]+([^&])"), "$1");
        // Followup param is a simple removal
        TextReplacer.ReplaceRegex(ref text, new(@"&ref=[0-9a-zA-Z+/=]+"), "");
    }

    public static void RemoveAllNavs(ref string text) => TextReplacer.ReplaceRegex(ref text, new(@"<nav[ >](?:(?!<nav[ >]).)*?</nav>", RegexOptions.Singleline), "");

    private static void ReplaceRegex(ref string text) { foreach (var x in s_regexReplaces) TextReplacer.ReplaceRegex(ref text, x.search, x.replacement); }
    private static void ReplaceText(ref string text) { foreach (var x in s_textReplaces) TextReplacer.ReplaceText(ref text, x.oldValue, x.newValue); }
    //private static void ReplaceTextFromFiles(ref string text) { foreach (string x in Directory.GetFiles(s_textReplacesFromDirFiles)) TextReplacer.ReplaceTextFromFile(ref text, x, TextReplacer.s_utf8Encoding, ""); }
}
