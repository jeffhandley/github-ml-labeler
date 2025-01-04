public static class DataFileUtils
{
    public static void EnsureOutputDirectory(string outputFile)
    {
        string? outputDir = Path.GetDirectoryName(outputFile);

        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
    }

    public static string SanitizeText(string text) => text
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Replace('\t', ' ')
        .Replace('"', '`');

    public static string SanitizeTextArray(string[] texts) => string.Join(" ", texts.Select(SanitizeText));

    public static string FormatIssueRecord(string label, string title, string body) =>
        string.Join('\t', [
            SanitizeText(label),
            SanitizeText(title),
            SanitizeText(body)
        ]);

    public static string FormatPullRequestRecord(string label, string title, string body, string[] fileNames, string[] folderNames) =>
        string.Join('\t', [
            SanitizeText(label),
            SanitizeText(title),
            SanitizeText(body),
            SanitizeTextArray(fileNames),
            SanitizeTextArray(folderNames)
        ]);
}
