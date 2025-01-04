using Microsoft.ML;
using Microsoft.ML.Data;
using GitHubClient;

var arguments = Args.Parse(args);
if (arguments is null) return;

(
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
) = arguments.Value;

List<Task> tasks = new();

if (issueModelPath is not null)
{
    tasks.Add(Task.Run(() => TestIssues()));
}

if (pullModelPath is not null)
{
    tasks.Add(Task.Run(() => TestPullRequests()));
}

await Task.WhenAll(tasks);

async Task TestIssues()
{
    var context = new MLContext();
    var model = context.Model.Load(issueModelPath, out _);
    var predictor = context.Model.CreatePredictionEngine<Issue, LabelPrediction>(model);
    int rowLimit = issueLimit ?? 50000;

    int matches = 0;
    int mismatches = 0;
    int noPrediction = 0;
    int noExisting = 0;

    async IAsyncEnumerable<(ulong Number, Issue Issue)> DownloadIssues()
    {
        if (githubToken is not null && org is not null && repo is not null)
        {
            await foreach (var result in GitHubApi.DownloadIssues(githubToken, org, repo, labelPredicate, rowLimit, 100, 1000, [30, 30, 30], false))
            {
                yield return (result.Issue.Number, new Issue(result.Issue));
            }
        }
    }

    async IAsyncEnumerable<(ulong Number, Issue Issue)> ReadIssues()
    {
        var allLines = File.ReadLinesAsync(issueDataPath);
        int rowNum = 0;

        await foreach (var line in allLines)
        {
            if (rowNum++ == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            yield return (
                (ulong)rowNum,
                new()
                {
                    Label = parts[0],
                    Labels = [parts[0]],
                    Title = parts[1],
                    Body = parts[2]
                }
            );

            if (rowNum > rowLimit)
            {
                break;
            }
        }
    }

    var issueList = issueDataPath is null ? DownloadIssues() : ReadIssues();

    await foreach (var result in issueList)
    {
        (string? PredictedLabel, string? ExistingLabel)? prediction = GetPrediction(
            predictor,
            result.Number,
            result.Issue,
            labelPredicate,
            "Issue");

        if (prediction is null)
        {
            continue;
        }

        if (prediction?.PredictedLabel is null && prediction?.ExistingLabel is not null)
        {
            noPrediction++;
        }
        else if (prediction?.PredictedLabel is not null && prediction?.ExistingLabel is null)
        {
            noExisting++;
        }
        else if (prediction?.PredictedLabel?.ToLower() == prediction?.ExistingLabel?.ToLower())
        {
            matches++;
        }
        else
        {
            mismatches++;
        }

        float total = matches + mismatches + noPrediction + noExisting;

        Console.WriteLine($"Issue #{result.Number} - Predicted: {(prediction?.PredictedLabel ?? "<NONE>")} - Existing: {(prediction?.ExistingLabel ?? "<NONE>")}");
        Console.WriteLine($"  Matches      : {matches} ({(float)matches / total:P2})");
        Console.WriteLine($"  Mismatches   : {mismatches} ({(float)mismatches / total:P2})");
        Console.WriteLine($"  No Prediction: {noPrediction} ({(float)noPrediction / total:P2})");
        Console.WriteLine($"  No Existing  : {noExisting} ({(float)noExisting / total:P2})");
    }

    Console.WriteLine("Test Complete");
}

async Task TestPullRequests()
{
    var context = new MLContext();
    var model = context.Model.Load(pullModelPath, out _);
    var predictor = context.Model.CreatePredictionEngine<PullRequest, LabelPrediction>(model);
    int rowLimit = pullLimit ?? 50000;

    List<(string PredictedLabel, string ExistingLabel, bool Match)>? testResults = new();

    int matches = 0;
    int mismatches = 0;
    int noPrediction = 0;
    int noExisting = 0;

    async IAsyncEnumerable<(ulong Number, PullRequest PullRequest)> DownloadPullRequests()
    {
        if (githubToken is not null && org is not null && repo is not null)
        {
            await foreach (var result in GitHubApi.DownloadPullRequests(githubToken, org, repo, labelPredicate, rowLimit, 25, 4000, [30, 30, 30], true))
            {
                yield return (result.PullRequest.Number, new PullRequest(result.PullRequest));
            }
        }
    }

    async IAsyncEnumerable<(ulong Number, PullRequest PullRequest)> ReadPullRequests()
    {
        var allLines = File.ReadLinesAsync(pullDataPath);
        int rowNum = 0;

        await foreach (var line in allLines)
        {
            if (rowNum++ == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            yield return (
                (ulong)rowNum,
                new()
                {
                    Label = parts[0],
                    Labels = [parts[0]],
                    Title = parts[1],
                    Body = parts[2],
                    FileNames = parts[3],
                    FolderNames = parts[4]
                }
            );

            if (rowNum > rowLimit)
            {
                break;
            }
        }
    }

    var pullList = pullDataPath is null ? DownloadPullRequests() : ReadPullRequests();

    await foreach (var result in pullList)
    {
        (string? PredictedLabel, string? ExistingLabel)? prediction = GetPrediction(
            predictor,
            result.Number,
            result.PullRequest,
            labelPredicate,
            "Pull Request");

        if (prediction is null)
        {
            continue;
        }

        if (prediction?.PredictedLabel is null && prediction?.ExistingLabel is not null)
        {
            noPrediction++;
        }
        else if (prediction?.PredictedLabel is not null && prediction?.ExistingLabel is null)
        {
            noExisting++;
        }
        else if (prediction?.PredictedLabel?.ToLower() == prediction?.ExistingLabel?.ToLower())
        {
            matches++;
        }
        else
        {
            mismatches++;
        }

        float total = matches + mismatches + noPrediction + noExisting;

        Console.WriteLine($"Pull Request #{result.Number} - Predicted: {(prediction?.PredictedLabel ?? "<NONE>")} - Existing: {(prediction?.ExistingLabel ?? "<NONE>")}");
        Console.WriteLine($"  Matches      : {matches} ({(float)matches / total:P2})");
        Console.WriteLine($"  Mismatches   : {mismatches} ({(float)mismatches / total:P2})");
        Console.WriteLine($"  No Prediction: {noPrediction} ({(float)noPrediction / total:P2})");
        Console.WriteLine($"  No Existing  : {noExisting} ({(float)noExisting / total:P2})");
    }

    Console.WriteLine("Test Complete");
}

(string? PredictedLabel, string? ExistingLabel)? GetPrediction<T>(PredictionEngine<T, LabelPrediction> predictor, ulong number, T issueOrPull, Predicate<string> labelPredicate, string itemType) where T : Issue
{
    var existing = issueOrPull.Labels?.FirstOrDefault(l => labelPredicate(l));

    if (existing is null && issueOrPull.HasMoreLabels)
    {
        Console.WriteLine($"{itemType} #{number} has too many labels. Cannot be sure no applicable label is already applied. Skipping.");
        return null;
    }

    var prediction = predictor.Predict(issueOrPull);

    if (prediction.Score is null || prediction.Score.Length == 0)
    {
        Console.WriteLine($"No prediction was made for {itemType} {org}/{repo}#{number}. Skipping.");
        return null;
    }

    VBuffer<ReadOnlyMemory<char>> labels = default;
    predictor.OutputSchema[nameof(LabelPrediction.Score)].GetSlotNames(ref labels);

    var predictions = prediction.Score
        .Select((score, index) => new
        {
            Score = score,
            Label = labels.GetItemOrDefault(index).ToString()
        })
        .OrderByDescending(p => p.Score)
        .Take(3);

    var bestScore = predictions.FirstOrDefault(p => p.Score >= threshold);
    string? predicted = bestScore?.Label;

    return (predicted, existing);
}
