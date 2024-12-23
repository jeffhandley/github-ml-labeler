using GitHubModel;

void ShowUsage() => Console.WriteLine("Expected: {org/repo} {github_token} [-i {path/to/issues.tsv}] [-p {path/to/pulls.tsv}] [--page-limit {pages}]");

if (args is null || args.Length < 4 || !args[0].Contains('/'))
{
    ShowUsage();
    return -1;
}

Queue<string> arguments = new(args);

string orgRepo = arguments.Dequeue();
string org = orgRepo.Split('/')[0];
string repo = orgRepo.Split('/')[1];
Console.WriteLine($"org/repo: {org}/{repo}");

string githubToken = arguments.Dequeue();

string? issuesPath = null;
string? pullsPath = null;
int pageLimit = 1000;

while (arguments.Count > 1)
{
    string option = arguments.Dequeue();

    switch (option)
    {
        case "-i":
            issuesPath = arguments.Dequeue();
            break;
        case "-p":
            pullsPath = arguments.Dequeue();
            break;
        case "--page-limit":
            pageLimit = int.Parse(arguments.Dequeue());
            break;
    }
}

if (arguments.Count == 1)
{
    ShowUsage();
    return -1;
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

Task.WaitAll(tasks);

return 0;

void EnsureOutputDirectory(string outputFile)
{
    string? outputDir = Path.GetDirectoryName(pullsPath);

    if (!string.IsNullOrEmpty(outputDir))
    {
        Directory.CreateDirectory(outputDir);
    }
}

async Task<bool> DownloadIssues(string outputPath)
{
    Console.WriteLine($"Issues Data Path: {outputPath}");

    byte perFlushCount = 0;

    using StreamWriter writer = new StreamWriter(outputPath);
    await foreach (var issue in GitHubClient.DownloadIssues(githubToken, org, repo, pageLimit))
    {
        writer.WriteLine(FormatIssueRecord(issue.Issue, issue.Label));

        if (++perFlushCount == 100)
        {
            writer.Flush();
            perFlushCount = 0;
        }
    }

    writer.Close();
    return true;
}

async Task<bool> DownloadPullRequests(string outputPath)
{
    Console.WriteLine($"Pulls Data Path: {outputPath}");

    byte perFlushCount = 0;

    using StreamWriter writer = new StreamWriter(outputPath);
    await foreach (var pullRequest in GitHubClient.DownloadPullRequests(githubToken, org, repo, pageLimit))
    {
        writer.WriteLine(FormatPullRequestRecord(pullRequest.PullRequest, pullRequest.Label));

        if (++perFlushCount == 100)
        {
            writer.Flush();
            perFlushCount = 0;
        }
    }

    writer.Close();
    return true;
}

static string SanitizeText(string text) => text
    .Replace('\r', ' ')
    .Replace('\n', ' ')
    .Replace('\t', ' ')
    .Replace('"', '`');

static string SanitizeTextArray(string[] texts) => string.Join(" ", texts.Select(SanitizeText));

static string FormatIssueRecord(Issue issue, string label) =>
    $"issue\t{issue.Number}\t{label}\t{SanitizeText(issue.Title)}\t{SanitizeText(issue.BodyText)}";

static string FormatPullRequestRecord(PullRequest pull, string label) =>
    $"pull\t{pull.Number}\t{label}\t{SanitizeText(pull.Title)}\t{SanitizeText(pull.BodyText)}\t{SanitizeTextArray(pull.FileNames)}\t{SanitizeTextArray(pull.FolderNames)}";
