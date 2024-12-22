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

    using StreamWriter writer = new StreamWriter(issuesDataPath);
    if (!await GitHubClient.DownloadIssues(githubToken, org, repo, writer, pageLimit)) return false;

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

    using StreamWriter writer = new StreamWriter(pullsDataPath);
    if (!await GitHubClient.DownloadPullRequests(githubToken, org, repo, writer, pageLimit)) return false;

    DirectoryInfo modelPath = new(Path.Join(currentDirectory, pullModelPath));
    Console.WriteLine($"Pulls Model Path: {modelPath.FullName}");

    if (!await ModelProcessor.PreparePullsData(org, repo, dataPath)) return false;
    if (!await ModelProcessor.TrainPullsModel(dataPath)) return false;
    if (!await ModelProcessor.TestFittedPullsModel(dataPath)) return false;

    return true;
}

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
