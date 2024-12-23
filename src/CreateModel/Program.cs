using GitHubModel;

void ShowUsage() => Console.WriteLine("Expected: {org/repo} {github_token} [-i {issues-model-output-path}] [-p {pulls-model-output-path}] [--page-limit {pages}]");

if (args is null || args.Length < 4 || !args[0].Contains('/'))
{
    ShowUsage();
    return -1;
}

Queue<string> arguments = new(args);

string orgRepo = arguments.Dequeue();
string org = orgRepo.Split('/')[0];
string repo = orgRepo.Split('/')[1];
Console.WriteLine($"Organization/Repository: {org}/{repo}");

string githubToken = arguments.Dequeue();

string? issueModelPath = null;
string? pullModelPath = null;
int pageLimit = 1000;

while (arguments.Count > 1)
{
    string option = arguments.Dequeue();

    switch (option)
    {
        case "-i":
            issueModelPath = arguments.Dequeue();
            break;
        case "-p":
            pullModelPath = arguments.Dequeue();
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

bool includeIssues = (issueModelPath is not null);
bool includePulls = (pullModelPath is not null);

string currentDirectory = Directory.GetCurrentDirectory();
DirectoryInfo dataPath = Directory.CreateDirectory(Path.Join(currentDirectory, "data"));

List<Task> tasks = new();

if (includeIssues)
{
    tasks.Add(Task.Run(() => CreateIssueModel()));
}

if (includePulls)
{
    tasks.Add(Task.Run(() => CreatePullRequestsModel()));
}

Task.WaitAll(tasks);

return 0;

async Task<bool> CreateIssueModel()
{
    string issuesDataPath = Path.Join(dataPath.FullName, "issues.tsv");
    Console.WriteLine($"Issues Data Path: {issuesDataPath}");

    byte perFlushCount = 0;

    using StreamWriter writer = new StreamWriter(issuesDataPath);
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

    DirectoryInfo modelPath = new(Path.Join(currentDirectory, issueModelPath));
    Console.WriteLine($"Issues Model Path: {modelPath.FullName}");

    if (!await ModelProcessor.PrepareIssueData(org, repo, dataPath)) return false;
    if (!await ModelProcessor.TrainIssueModel(dataPath)) return false;
    if (!await ModelProcessor.TestFittedIssueModel(dataPath)) return false;

    return true;
}

async Task<bool> CreatePullRequestsModel()
{
    string pullsDataPath = Path.Join(dataPath.FullName, "pulls.tsv");
    Console.WriteLine($"Pulls Data Path: {pullsDataPath}");

    byte perFlushCount = 0;

    using StreamWriter writer = new StreamWriter(pullsDataPath);
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

    DirectoryInfo modelPath = new(Path.Join(currentDirectory, pullModelPath));
    Console.WriteLine($"Pulls Model Path: {modelPath.FullName}");

    if (!await ModelProcessor.PreparePullsData(org, repo, dataPath)) return false;
    if (!await ModelProcessor.TrainPullsModel(dataPath)) return false;
    if (!await ModelProcessor.TestFittedPullsModel(dataPath)) return false;

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

static class ModelProcessor
{
    public static async Task<bool> PrepareIssueData(string org, string repo, DirectoryInfo dataPath)
    {
        await Task.Delay(500);
        return true;
    }

    public static async Task<bool> TrainIssueModel(DirectoryInfo dataPath)
    {
        await Task.Delay(1_000);
        return true;
    }

    public static async Task<bool> TestFittedIssueModel(DirectoryInfo dataPath)
    {
        await Task.Delay(250);
        return true;
    }

    public static async Task<bool> PreparePullsData(string org, string repo, DirectoryInfo dataPath)
    {
        await Task.Delay(750);
        return true;
    }

    public static async Task<bool> TrainPullsModel(DirectoryInfo dataPath)
    {
        await Task.Delay(1_500);
        return true;
    }

    public static async Task<bool> TestFittedPullsModel(DirectoryInfo dataPath)
    {
        await Task.Delay(500);
        return true;
    }
}
