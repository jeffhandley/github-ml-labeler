using Microsoft.ML.Data;

public class Issue
{
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }

    // The Area and Description properties allow loading
    // models from the issue-labeler implementation
    public string? Area { get => Label; }
    public string? Description { get => Body; }

    [NoColumn]
    public string[]? Labels { get; set; }

    [NoColumn]
    public bool HasMoreLabels { get; set; }

    public Issue() { }

    public Issue(GitHubClient.Issue issue)
    {
        Title = issue.Title;
        Body = issue.Body;
        Labels = issue.LabelNames;
        HasMoreLabels = issue.Labels.HasNextPage;
    }
}

public class PullRequest : Issue
{
    public string? FileNames { get; set; }
    public string? FolderNames { get; set; }

    public PullRequest() { }

    public PullRequest(GitHubClient.PullRequest pull) : base(pull)
    {
        FileNames = string.Join(' ', pull.FileNames);
        FolderNames = string.Join(' ', pull.FolderNames);
    }
}

public class LabelPrediction
{
    public string? PredictedLabel { get; set; }
    public float[]? Score { get; set; }
}
