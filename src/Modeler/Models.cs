public class Issue
{
    public required ulong Number { get; set; }
    public string? Label { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
}

public class PullRequest : Issue
{
    public string? FileNames { get; set; }
    public string? FolderNames { get; set; }
}

public class LabelPrediction
{
    public string? PredictedLabel { get; set; }
}
