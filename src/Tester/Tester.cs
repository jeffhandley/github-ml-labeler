﻿using Microsoft.ML;
using Microsoft.ML.Data;
using GitHubClient;

void ShowUsage(string? message = null)
{
    Console.WriteLine($"Invalid or missing arguments.{(message is null ? "" : " " + message)}");
    Console.WriteLine("  --threshold {threshold}");
    Console.WriteLine("  [--token {github_token} --repo {org}/{repo} --label-prefix {label-prefix}]");
    Console.WriteLine("  [--issue-data {path/to/issue-data.tsv}");
    Console.WriteLine("  [--issue-model {path/to/issue-model.zip} --issue-limit {issues}]");
    Console.WriteLine("  [--pull-data {path/to/pull-data.tsv}");
    Console.WriteLine("  [--pull-model {path/to/pull-model.zip} --pull-limit {pulls}]");

    Environment.Exit(-1);
}

Queue<string> arguments = new(args);
string? org = null;
string? repo = null;
string? githubToken = null;
string? issueDataPath = null;
string? issueModelPath = null;
int? issueLimit = null;
string? pullDataPath = null;
string? pullModelPath = null;
int? pullLimit = null;
float? threshold = null;
Predicate<string>? labelPredicate = null;

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
            issueDataPath = arguments.Dequeue();
            break;
        case "--issue-model":
            issueModelPath = arguments.Dequeue();
            break;
        case "--issue-limit":
            issueLimit = int.Parse(arguments.Dequeue());
            break;
        case "--pull-data":
            pullDataPath = arguments.Dequeue();
            break;
        case "--pull-model":
            pullModelPath = arguments.Dequeue();
            break;
        case "--pull-limit":
            pullLimit = int.Parse(arguments.Dequeue());
            break;
        case "--label-prefix":
            string labelPrefix = arguments.Dequeue();
            labelPredicate = label => label.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase);
            break;
        case "--threshold":
            threshold = float.Parse(arguments.Dequeue());
            break;
        default:
            ShowUsage($"Unrecognized argument: {argument}");
            return;
    }
}

if (
    threshold is null || labelPredicate is null ||
    (
        issueDataPath is null && pullDataPath is null &&
        (org is null || repo is null || githubToken is null)
    ) ||
    (issueModelPath is null && pullModelPath is null))
{
    ShowUsage();
    return;
}

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

            if (rowNum >= rowLimit)
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

            if (rowNum >= rowLimit)
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
