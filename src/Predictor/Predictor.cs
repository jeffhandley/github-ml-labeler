using Microsoft.ML;
using Microsoft.ML.Data;
using GitHubClient;

var arguments = Args.Parse(args);
if (arguments is null) return;
(
    string org,
    string repo,
    string githubToken,
    string? issueModelPath,
    List<ulong>? issueNumbers,
    string? pullModelPath,
    List<ulong>? pullNumbers,
    float threshold,
    Func<string, bool> labelPredicate,
    string? defaultLabel,
    bool test
) = arguments.Value;

List<Task> tasks = new();

if (issueModelPath is not null && issueNumbers is not null)
{
    foreach (ulong issueNumber in issueNumbers)
    {
        var result = await GitHubApi.GetIssue(githubToken, org, repo, issueNumber);

        if (result is null)
        {
            Console.WriteLine($"Issue #{issueNumber} was not found. Skipping.");
            continue;
        }

        tasks.Add(Task.Run(() => ProcessPrediction(
            issueModelPath,
            issueNumber,
            new Issue(result),
            labelPredicate,
            defaultLabel,
            ModelType.Issue,
            test
        )));
    }
}

if (pullModelPath is not null && pullNumbers is not null)
{
    foreach (ulong pullNumber in pullNumbers)
    {
        var result = await GitHubApi.GetPullRequest(githubToken, org, repo, pullNumber);

        if (result is null)
        {
            Console.WriteLine($"Pull Request #{pullNumber} was not found. Skipping.");
            continue;
        }

        tasks.Add(Task.Run(() => ProcessPrediction(
            pullModelPath,
            pullNumber,
            new PullRequest(result),
            labelPredicate,
            defaultLabel,
            ModelType.PullRequest,
            test
        )));
    }
}

await Task.WhenAll(tasks);

async Task ProcessPrediction<T>(string modelPath, ulong number, T issueOrPull, Func<string, bool> labelPredicate, string? defaultLabel, ModelType type, bool test) where T : Issue
{
    if (issueOrPull.HasMoreLabels)
    {
        Console.WriteLine($"{type} #{number} has too many labels applied already. Cannot be sure no applicable label is already applied.");
        return;
    }

    var applicableLabel = issueOrPull.Labels?.FirstOrDefault(labelPredicate);

    bool hasDefaultLabel =
        (defaultLabel is not null) &&
        (issueOrPull.Labels?.Any(l => l.Equals(defaultLabel, StringComparison.OrdinalIgnoreCase)) ?? false);

    if (applicableLabel is not null)
    {
        Console.WriteLine($"{type} #{number} already has an applicable label '{applicableLabel}'.");

        if (hasDefaultLabel)
        {
            Console.WriteLine($"Removing the default label '{defaultLabel}' from {type} #{number} since it has an applicable label.");

            if (!test && defaultLabel is not null)
            {
                await GitHubApi.RemoveLabel(githubToken, org, repo, number, defaultLabel);
            }
        }

        return;
    }

    var context = new MLContext();
    var model = context.Model.Load(modelPath, out _);
    var predictor = context.Model.CreatePredictionEngine<T, LabelPrediction>(model);
    var prediction = predictor.Predict(issueOrPull);

    if (prediction.Score is null || prediction.Score.Length == 0)
    {
        Console.WriteLine($"No prediction was made for {type} #{number}.");
        return;
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

    Console.WriteLine($"Label predictions for {type} #{number}:");

    foreach (var pred in predictions)
    {
        Console.WriteLine($"  {type} #{number} - Label: {pred.Label} - Score: {pred.Score}");
    }

    var bestScore = predictions.FirstOrDefault(p => p.Score >= threshold);

    if (bestScore is not null)
    {
        Console.WriteLine($"Predicted label for {type} #{number}: {bestScore.Label}");

        if (!test)
        {
            await GitHubApi.AddLabel(githubToken, org, repo, number, bestScore.Label);
        }

        if (hasDefaultLabel && defaultLabel is not null)
        {
            Console.WriteLine($"Removing default label '{defaultLabel}' from {type} #{number}.");

            if (!test)
            {
                await GitHubApi.RemoveLabel(githubToken, org, repo, number, defaultLabel);
            }
        }
    }
    else
    {
        Console.WriteLine($"No label score met the specified threshold of {threshold}.");

        if (defaultLabel is not null)
        {
            Console.WriteLine($"Using default label '{defaultLabel}' for {type} #{number}.");

            if (!test)
            {
                await GitHubApi.AddLabel(githubToken, org, repo, number, defaultLabel);
            }
        }
    }
}
