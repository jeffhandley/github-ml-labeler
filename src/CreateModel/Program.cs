if (args is null || args.Length < 4 || !args[0].Contains('/'))
{
    Console.WriteLine("Expected: {org/repo} {github_token} [-i {issue-model-output-path}] [-p {pulls-model-output-path}]");
    return -1;
}

string org = args[0].Split('/')[0];
string rep = args[0].Split('/')[1];
string githubToken = args[1];

Console.WriteLine($"Organization/Repository: {org}/{rep}");

string currentDirectory = Directory.GetCurrentDirectory();
string? issuePathArg = args.Length >= 6 && args[4] == "-i" ? args[5] : args[2] == "-i" ? args[3] : null;
string? pullsPathArg = args.Length >= 6 && args[4] == "-p" ? args[5] : args[2] == "-p" ? args[3] : null;

bool includeIssue = (issuePathArg is not null);
bool includePulls = (pullsPathArg is not null);

DirectoryInfo? issueModelPath = issuePathArg?.Trim() switch
{
    null or "" => null,
    _ => new(Path.Join(currentDirectory, issuePathArg)),
};

DirectoryInfo? pullsModelPath = pullsPathArg?.Trim() switch
{
    null or "" => null,
    _ => new(Path.Join(currentDirectory, pullsPathArg)),
};

List<Task> tasks = new();

if (includeIssue)
{
    DirectoryInfo issueDataPath = Directory.CreateDirectory(Path.Join(currentDirectory, "data", "issue"));
    Console.WriteLine($"Issue Data Path:  {issueDataPath.FullName}");
    Console.WriteLine($"Issue Model Path: {issueModelPath?.FullName}");

    tasks.Add(CreateIssueModel(org, rep, issueDataPath));
}

if (includePulls)
{
    DirectoryInfo pullsDataPath = Directory.CreateDirectory(Path.Join(currentDirectory, "data", "pulls"));
    Console.WriteLine($"Pulls Data Path:  {pullsDataPath.FullName}");
    Console.WriteLine($"Pulls Model Path: {pullsModelPath?.FullName}");

    tasks.Add(CreatePullsModel(org, rep, pullsDataPath));
}

Task.WaitAll(tasks);

return 0;

async Task<bool> CreateIssueModel(string org, string rep, DirectoryInfo dataPath)
{
    if (!await DownloadHelper.IssueDownload(org, rep, dataPath)) return false;
    if (!await ModelProcessor.PrepareIssueData(org, rep, dataPath)) return false;
    if (!await ModelProcessor.TrainIssueModel(dataPath)) return false;
    if (!await ModelProcessor.TestFittedIssueModel(dataPath)) return false;

    return true;
}

async Task<bool> CreatePullsModel(string org, string rep, DirectoryInfo dataPath)
{
    if (!await DownloadHelper.PullsDownload(org, rep, dataPath)) return false;
    if (!await ModelProcessor.PreparePullsData(org, rep, dataPath)) return false;
    if (!await ModelProcessor.TrainPullsModel(dataPath)) return false;
    if (!await ModelProcessor.TestFittedPullsModel(dataPath)) return false;

    return true;
}

static class DownloadHelper
{
    public static async Task<bool> IssueDownload(string org, string rep, DirectoryInfo dataPath)
    {
        await Task.Delay(5_000);
        return true;
    }

    public static async Task<bool> PullsDownload(string org, string rep, DirectoryInfo dataPath)
    {
        await Task.Delay(15_000);
        return true;
    }
}

static class ModelProcessor
{
    public static async Task<bool> PrepareIssueData(string org, string rep, DirectoryInfo dataPath)
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

    public static async Task<bool> PreparePullsData(string org, string rep, DirectoryInfo dataPath)
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
