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
        tasks.Add(Task.Run(() => ProcessPrediction(
            issueModelPath,
            issueNumber,
            async () => new Issue(await GitHubApi.GetIssue(githubToken, org, repo, issueNumber)),
            labelPredicate,
            ModelType.Issue,
            test
        )));
    }
}

if (pullModelPath is not null && pullNumbers is not null)
{
    foreach (ulong pullNumber in pullNumbers)
    {
        tasks.Add(Task.Run(() => ProcessPrediction(
            pullModelPath,
            pullNumber,
            async () => new PullRequest(await GitHubApi.GetPullRequest(githubToken, org, repo, pullNumber)),
            labelPredicate,
            ModelType.PullRequest,
            test
        )));
    }
}

await Task.WhenAll(tasks);

async Task ProcessPrediction<T>(string modelPath, ulong number, Func<Task<T>> getItem, Func<string, bool> labelPredicate, ModelType type, bool test) where T : Issue
{
    var issueOrPull = await getItem();

    if (issueOrPull.HasMoreLabels)
    {
        Console.WriteLine($"{type} #{number} has too many labels applied already. Cannot be sure no applicable label is already applied. Aborting.");
        return;
    }

    var applicableLabel = issueOrPull.Labels?.FirstOrDefault(labelPredicate);

    if (applicableLabel is not null)
    {
        Console.WriteLine($"{type} #{number} already has an applicable label '{applicableLabel}'. Aborting.");
        return;
    }

    var context = new MLContext();
    var model = context.Model.Load(modelPath, out _);
    var predictor = context.Model.CreatePredictionEngine<T, LabelPrediction>(model);
    var prediction = predictor.Predict(issueOrPull);

    if (prediction.Score is null || prediction.Score.Length == 0)
    {
        Console.WriteLine($"No prediction was made for {type} {org}/{repo}#{number}");
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

    Console.WriteLine($"Label predictions for {type} {org}/{repo}#{number}:");

    foreach (var pred in predictions)
    {
        Console.WriteLine($"  Label: {pred.Label} - Score: {pred.Score}");
    }

    var bestScore = predictions.FirstOrDefault(p => p.Score >= threshold);

    if (bestScore is not null)
    {
        Console.WriteLine($"Predicted Label: {bestScore.Label}");

        if (!test)
        {
            await GitHubApi.AddLabel(githubToken, org, repo, number, bestScore.Label);
        }
    }
    else
    {
        Console.WriteLine($"No label score met the specified threshold of {threshold}.");

        if (defaultLabel is not null)
        {
            Console.WriteLine($"Using default label: {defaultLabel}");

            if (!test)
            {
                await GitHubApi.AddLabel(githubToken, org, repo, number, defaultLabel);
            }
        }
    }
}
