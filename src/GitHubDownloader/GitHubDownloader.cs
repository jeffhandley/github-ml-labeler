using GitHubClient;

void ShowUsage(string? message = null)
{
    Console.WriteLine($"Invalid or missing arguments.{(message is null ? "" : " " + message)}");
    Console.WriteLine("  --token {github_token}");
    Console.WriteLine("  --repo {org/repo}");
    Console.WriteLine("  --label-prefix {label-prefix}");
    Console.WriteLine("  [--issue-data {path/to/issues.tsv}]");
    Console.WriteLine("  [--pull-data {path/to/pulls.tsv}]");
    Console.WriteLine("  [--page-limit {pages=500}]");
    Console.WriteLine("  [--retries {comma-separated-retries-in-seconds}]");
    Console.WriteLine("  [--verbose]");

    Environment.Exit(-1);
}

Queue<string> arguments = new(args);
string? org = null;
string? repo = null;
string? githubToken = null;
string? issuesPath = null;
string? pullsPath = null;
int pageLimit = 500;
int[] retries = [10, 20, 30, 60, 120];
Predicate<string>? labelPredicate = null;
bool verbose = false;

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
                return;
            }

            string[] parts = orgRepo.Split('/');
            org = parts[0];
            repo = parts[1];
            break;
        case "--issue-data":
            issuesPath = arguments.Dequeue();
            break;
        case "--pull-data":
            pullsPath = arguments.Dequeue();
            break;
        case "--page-limit":
            pageLimit = int.Parse(arguments.Dequeue());
            break;
        case "--retries":
            retries = arguments.Dequeue().Split(',').Select(r => int.Parse(r)).ToArray();
            break;
        case "--label-prefix":
            string labelPrefix = arguments.Dequeue();
            labelPredicate = (label) => label.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase);
            break;
        case "--verbose":
            verbose = true;
            break;
        default:
            ShowUsage($"Unrecognized argument: {argument}");
            return;
    }
}

if (org is null || repo is null || githubToken is null || labelPredicate is null ||
    (issuesPath is null && pullsPath is null))
{
    ShowUsage();
    return;
}

List<Task> tasks = new();

if (!string.IsNullOrEmpty(issuesPath))
{
    EnsureOutputDirectory(issuesPath);
    tasks.Add(Task.Run(() => DownloadIssues(issuesPath)));
}

if (!string.IsNullOrEmpty(pullsPath))
{
    EnsureOutputDirectory(pullsPath);
    tasks.Add(Task.Run(() => DownloadPullRequests(pullsPath)));
}

await Task.WhenAll(tasks);

void EnsureOutputDirectory(string outputFile)
{
    string? outputDir = Path.GetDirectoryName(outputFile);

    if (!string.IsNullOrEmpty(outputDir))
    {
        Directory.CreateDirectory(outputDir);
    }
}

async Task DownloadIssues(string outputPath)
{
    Console.WriteLine($"Issues Data Path: {outputPath}");

    byte perFlushCount = 0;

    using StreamWriter writer = new StreamWriter(outputPath);
    writer.WriteLine(string.Join('\t', "Number", "Label", "Title", "Body"));

    await foreach (var issue in GitHubApi.DownloadIssues(githubToken, org, repo, labelPredicate, pageLimit, retries, verbose))
    {
        writer.WriteLine(FormatIssueRecord(issue.Issue, issue.Label));

        if (++perFlushCount == 100)
        {
            writer.Flush();
            perFlushCount = 0;
        }
    }

    writer.Close();
}

async Task DownloadPullRequests(string outputPath)
{
    Console.WriteLine($"Pulls Data Path: {outputPath}");

    byte perFlushCount = 0;

    using StreamWriter writer = new StreamWriter(outputPath);
    writer.WriteLine(string.Join('\t', "Number", "Label", "Title", "Body", "FileNames", "FolderNames"));

    await foreach (var pullRequest in GitHubApi.DownloadPullRequests(githubToken, org, repo, labelPredicate, pageLimit, retries, verbose))
    {
        writer.WriteLine(FormatPullRequestRecord(pullRequest.PullRequest, pullRequest.Label));

        if (++perFlushCount == 100)
        {
            writer.Flush();
            perFlushCount = 0;
        }
    }

    writer.Close();
}

static string SanitizeText(string text) => text
    .Replace('\r', ' ')
    .Replace('\n', ' ')
    .Replace('\t', ' ')
    .Replace('"', '`');

static string SanitizeTextArray(string[] texts) => string.Join(" ", texts.Select(SanitizeText));

static string FormatIssueRecord(Issue issue, string label) =>
    $"{issue.Number}\t{label}\t{SanitizeText(issue.Title)}\t{SanitizeText(issue.Body)}";

static string FormatPullRequestRecord(PullRequest pull, string label) =>
    $"{pull.Number}\t{label}\t{SanitizeText(pull.Title)}\t{SanitizeText(pull.Body)}\t{SanitizeTextArray(pull.FileNames)}\t{SanitizeTextArray(pull.FolderNames)}";
