using Microsoft.ML;
using Microsoft.ML.Data;
using GitHubClient;

void ShowUsage(string? message = null)
{
    Console.WriteLine($"Invalid or missing arguments.{(message is null ? "" : " " + message)}");
    Console.WriteLine("  --token {github_token}");
    Console.WriteLine("  --repo {org}/{repo}");
    Console.WriteLine("  --label-prefix {label-prefix}");
    Console.WriteLine("  --threshold {threshold}");
    Console.WriteLine("  [--issue-model {path/to/issue-model.zip} --issue {issue-number}]");
    Console.WriteLine("  [--pull-model {path/to/pull-model.zip} --pull {pull-number}]");
    Console.WriteLine("  [--default-label {needs-area-label}]");

    Environment.Exit(-1);
}

Queue<string> arguments = new(args);
string? org = null;
string? repo = null;
string? githubToken = null;
string? issueModelPath = null;
ulong? issueNumber = null;
string? pullModelPath = null;
ulong? pullNumber = null;
float? threshold = null;
Func<string, bool>? labelPredicate = null;
string? defaultLabel = null;

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
        case "--issue-model":
            issueModelPath = arguments.Dequeue();
            break;
        case "--issue":
            issueNumber = ulong.Parse(arguments.Dequeue());
            break;
        case "--pull-model":
            pullModelPath = arguments.Dequeue();
            break;
        case "--pull":
            pullNumber = ulong.Parse(arguments.Dequeue());
            break;
        case "--label-prefix":
            string labelPrefix = arguments.Dequeue();
            labelPredicate = label => label.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase);
            break;
        case "--threshold":
            threshold = float.Parse(arguments.Dequeue());
            break;
        case "--default-label":
            defaultLabel = arguments.Dequeue();
            break;
        default:
            ShowUsage($"Unrecognized argument: {argument}");
            return;
    }
}

if (org is null || repo is null || githubToken is null || threshold is null || labelPredicate is null ||
    (issueModelPath is null != issueNumber is null) ||
    (pullModelPath is null != pullNumber is null) ||
    (issueModelPath is null && pullModelPath is null))
{
    ShowUsage();
    return;
}

if (issueModelPath is not null && issueNumber is not null)
{
    await ProcessPrediction(
        issueModelPath,
        issueNumber.Value,
        async () => new Issue(await GitHubApi.GetIssue(githubToken, org, repo, issueNumber.Value)),
        labelPredicate,
        "issue");
}

if (pullModelPath is not null && pullNumber is not null)
{
    await ProcessPrediction(
        pullModelPath,
        pullNumber.Value,
        async () => new PullRequest(await GitHubApi.GetPullRequest(githubToken, org, repo, pullNumber.Value)),
        labelPredicate,
        "pull request");
}

async Task ProcessPrediction<T>(string modelPath, ulong number, Func<Task<T>> getItem, Func<string, bool> labelPredicate, string itemType) where T : Issue
{
    var issueOrPull = await getItem();

    if (issueOrPull.HasMoreLabels)
    {
        Console.WriteLine($"{itemType} #{number} has too many labels applied already. Cannot be sure no applicable label is already applied. Aborting.");
        return;
    }

    var applicableLabel = issueOrPull.Labels?.FirstOrDefault(labelPredicate);

    if (applicableLabel is not null)
    {
        Console.WriteLine($"{itemType} #{number} already has an applicable label '{applicableLabel}'. Aborting.");
        return;
    }

    var context = new MLContext();
    var model = context.Model.Load(modelPath, out _);
    var predictor = context.Model.CreatePredictionEngine<T, LabelPrediction>(model);
    var prediction = predictor.Predict(issueOrPull);

    if (prediction.Score is null || prediction.Score.Length == 0)
    {
        Console.WriteLine($"No prediction was made for {itemType} {org}/{repo}#{number}");
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

    Console.WriteLine($"Label predictions for {itemType} {org}/{repo}#{number}:");

    foreach (var pred in predictions)
    {
        Console.WriteLine($"  Label: {pred.Label} - Score: {pred.Score}");
    }

    var bestScore = predictions.FirstOrDefault(p => p.Score >= threshold);

    if (bestScore is not null)
    {
        Console.WriteLine($"Predicted Label: {bestScore.Label}");
        await GitHubApi.AddLabel(githubToken, org, repo, number, bestScore.Label);
    }
    else
    {
        Console.WriteLine($"No label score met the specified threshold of {threshold}.");

        if (defaultLabel is not null)
        {
            Console.WriteLine($"Applying default label: {defaultLabel}");
            await GitHubApi.AddLabel(githubToken, org, repo, number, defaultLabel);
        }
    }
}
