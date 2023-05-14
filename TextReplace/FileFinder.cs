namespace TextReplace;

internal static class FileFinder
{
    public static FileInfo[] FindFilesRecursive(DirectoryInfo dir, string fileSearchPattern)
    {
        return dir.GetFiles(fileSearchPattern, SearchOption.AllDirectories);
    }
}
