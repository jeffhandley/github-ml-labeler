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
