using System.Text.RegularExpressions;

namespace TextReplaceTest;

public class RegexTest
{
    [Fact]
    public void TestCsrfKey()
    {
        Assert.Matches(@"\?csrfKey=[0-9a-z]+", @"?csrfKey=a73c5ddb684838618485e643616c8d4c");
        Assert.Matches(@"\?csrfKey=[0-9a-z]+", @"href?csrfKey=a73c5ddb684838618485e643616c8d4c""");
    }

    [Fact]
    public void TestRefKey()
    {
        var r = new Regex(@"\?ref=[0-9a-zA-Z+/=]+([^&])");
        var input = @"<a href='login/?ref=aHR0cHM6Ly9mb3J1bXMubXVtYmxlLmluZm8vdG9waWMvMTg0MTEtYnVpbGRpbmctb24td2luZG93czEwLXdpdGgtbXN5czJtaW5ndy8jcmVwbHlGb3Jt' data-ipsDialog";
        Assert.Matches(r, input);
        var actual = r.Replace(input, "$1");
        Assert.Equal(@"<a href='login/' data-ipsDialog", actual);
    }

    [Fact]
    public void TestFindUrl()
    {
        Regex regex = new(@"((href)|(src))=[""'](?<url>[^""']+)[""']");
        var input = """
            <div><a href="1"></a><a href='2'></a><script src="3"></script><script src="4"></script></div>
            """;
        var urls = regex.Matches(input).Select(x => x.Groups["url"].Value).ToArray();
        Assert.Equal(new string[] { "1", "2", "3", "4", }, urls);
    }

    [Fact]
    public void TestNavRemoval()
    {
        var text = """
            <div>
                <nav a b>
                    <ul>
                        <li><nav >.</nav>
                    </ul>
                </nav>
            </div>
            """;
        var regex = new Regex(@"<nav[ >](?:(?!<nav[ >]).)*?</nav>", RegexOptions.Singleline);
        Assert.Equal(@"<nav >.</nav>", regex.Match(text).Value);
        Assert.Equal("""
            <div>
                <nav a b>
                    <ul>
                        <li>
                    </ul>
                </nav>
            </div>
            """, regex.Replace(input: text, ""));
    }
}
