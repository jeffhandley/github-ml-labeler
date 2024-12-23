public class Issue
{
    [LoadColumn(0)]
    public required uint Number { get; set; }

    [LoadColumn(1)]
    public string? Label { get; set; }

    [LoadColumn(2)]
    public required string Title { get; set; }

    [LoadColumn(3)]
    public string? Body { get; set; }
}

public class PullRequest : Issue
{
    [LoadColumn(4)]
    public string? FileNames { get; set; }

    [LoadColumn(5)]
    public string? FolderNames { get; set; }
}

public class LabelPrediction
{
    public string? Label { get; set; }
}
