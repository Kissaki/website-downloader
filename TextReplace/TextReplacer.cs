namespace TextReplace;

internal static class TextReplacer
{
    public static readonly Encoding s_utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static void ReplaceRegex(ref string text, Regex r, string replacement) => text = r.Replace(text, replacement);

    internal static void ReplaceText(ref string text, string oldText, string newText) => text = text.Replace(oldText, newText);

    /// <param name="encoding">null defaults to utf8</param>
    public static void ReplaceTextFromFile(ref string text, string oldvalueTextfilepath, Encoding? encoding, string? newText)
    {
        var oldText = File.ReadAllText(oldvalueTextfilepath, encoding ?? s_utf8Encoding);
        text = text.Replace(oldText, newText);
    }
}
