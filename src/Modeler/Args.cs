static class Args
{
    static void ShowUsage(string? message = null)
    {
        Console.WriteLine($"Invalid or missing arguments.{(message is null ? "" : " " + message)}");
        Console.WriteLine("  [--issue-data {path/to/issue-data.tsv}]");
        Console.WriteLine("  [--issue-model {path/to/issue-model.zip}]");
        Console.WriteLine("  [--pull-data {path/to/pull-data.tsv}]");
        Console.WriteLine("  [--pull-model {path/to/pull-model.zip}]");

        Environment.Exit(-1);
    }

    public static (
        string? IssueDataPath,
        string? IssueModelPath,
        string? PullDataPath,
        string? PullModelPath
    )
    Parse(string[] args)
    {
        Queue<string> arguments = new(args);
        string? issueDataPath = null;
        string? issueModelPath = null;
        string? pullDataPath = null;
        string? pullModelPath = null;

        while (arguments.Count > 0)
        {
            string argument = arguments.Dequeue();

            switch (argument)
            {
                case "--issue-data":
                    issueDataPath = arguments.Dequeue();
                    break;
                case "--issue-model":
                    issueModelPath = arguments.Dequeue();
                    break;
                case "--pull-data":
                    pullDataPath = arguments.Dequeue();
                    break;
                case "--pull-model":
                    pullModelPath = arguments.Dequeue();
                    break;
                default:
                    ShowUsage($"Unrecognized argument: {argument}");
                    return (null, null, null, null);
            }
        }

        if ((issueDataPath is null != issueModelPath is null) ||
            (pullDataPath is null != pullModelPath is null) ||
            (issueModelPath is null && pullModelPath is null))
        {
            ShowUsage();
            return (null, null, null, null);
        }

        return (issueDataPath, issueModelPath, pullDataPath, pullModelPath);
    }
}
