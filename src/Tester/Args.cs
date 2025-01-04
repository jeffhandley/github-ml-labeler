public static class Args
{
    public static void ShowUsage(string? message = null)
    {
        Console.WriteLine($"Invalid or missing arguments.{(message is null ? "" : " " + message)}");
        Console.WriteLine("  --threshold {threshold}");
        Console.WriteLine("  [--token {github_token} --repo {org}/{repo} --label-prefix {label-prefix}]");
        Console.WriteLine("  [--issue-data {path/to/issue-data.tsv}");
        Console.WriteLine("  [--issue-model {path/to/issue-model.zip} --issue-limit {issues}]");
        Console.WriteLine("  [--pull-data {path/to/pull-data.tsv}");
        Console.WriteLine("  [--pull-model {path/to/pull-model.zip} --pull-limit {pulls}]");

        Environment.Exit(-1);
    }

    public static (
        string? org,
        string? repo,
        string? githubToken,
        string? issueDataPath,
        string? issueModelPath,
        int? issueLimit,
        string? pullDataPath,
        string? pullModelPath,
        int? pullLimit,
        float threshold,
        Predicate<string> labelPredicate
    )?
    Parse(string[] args)
    {
        Queue<string> arguments = new(args);
        string? org = null;
        string? repo = null;
        string? githubToken = null;
        string? issueDataPath = null;
        string? issueModelPath = null;
        int? issueLimit = null;
        string? pullDataPath = null;
        string? pullModelPath = null;
        int? pullLimit = null;
        float? threshold = null;
        Predicate<string>? labelPredicate = null;

        while (arguments.Count > 0)
        {
            string argument = arguments.Dequeue();

            switch (argument)
            {
                case "--token":
                    githubToken = arguments.Dequeue();
                    break;
                case "--repo":
                    string orgRepo = arguments.Dequeue();

                    if (!orgRepo.Contains('/'))
                    {
                        ShowUsage($$"""Argument 'repo' is not in the format of '{org}/{repo}': {{orgRepo}}""");
                        return null;
                    }

                    string[] parts = orgRepo.Split('/');
                    org = parts[0];
                    repo = parts[1];
                    break;
                case "--issue-data":
                    issueDataPath = arguments.Dequeue();
                    break;
                case "--issue-model":
                    issueModelPath = arguments.Dequeue();
                    break;
                case "--issue-limit":
                    issueLimit = int.Parse(arguments.Dequeue());
                    break;
                case "--pull-data":
                    pullDataPath = arguments.Dequeue();
                    break;
                case "--pull-model":
                    pullModelPath = arguments.Dequeue();
                    break;
                case "--pull-limit":
                    pullLimit = int.Parse(arguments.Dequeue());
                    break;
                case "--label-prefix":
                    string labelPrefix = arguments.Dequeue();
                    labelPredicate = label => label.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase);
                    break;
                case "--threshold":
                    threshold = float.Parse(arguments.Dequeue());
                    break;
                default:
                    ShowUsage($"Unrecognized argument: {argument}");
                    return null;
            }
        }

        if (
            threshold is null || labelPredicate is null ||
            (
                issueDataPath is null && pullDataPath is null &&
                (org is null || repo is null || githubToken is null)
            ) ||
            (issueModelPath is null && pullModelPath is null)
        )
        {
            ShowUsage();
            return null;
        }

        return (
            org,
            repo,
            githubToken,
            issueDataPath,
            issueModelPath,
            issueLimit,
            pullDataPath,
            pullModelPath,
            pullLimit,
            (float)threshold,
            (Predicate<string>)labelPredicate
        );
    }
}
