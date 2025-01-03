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
    Console.WriteLine("  [--test]");

    Environment.Exit(-1);
}

Queue<string> arguments = new(args);
string? org = null;
string? repo = null;
string? githubToken = null;
string? issueModelPath = null;
List<ulong>? issueNumbers = null;
string? pullModelPath = null;
List<ulong>? pullNumbers = null;
float? threshold = null;
Func<string, bool>? labelPredicate = null;
string? defaultLabel = null;
bool test = false;

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
            issueNumbers ??= new();
            issueNumbers.Add(ulong.Parse(arguments.Dequeue()));
            break;
        case "--pull-model":
            pullModelPath = arguments.Dequeue();
            break;
        case "--pull":
            pullNumbers ??= new();
            pullNumbers.Add(ulong.Parse(arguments.Dequeue()));
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
        case "--test":
            test = true;
            break;
        default:
            ShowUsage($"Unrecognized argument: {argument}");
            return;
    }
}

if (org is null || repo is null || githubToken is null || threshold is null || labelPredicate is null ||
    (issueModelPath is null != issueNumbers is null) ||
    (pullModelPath is null != pullNumbers is null) ||
    (issueModelPath is null && pullModelPath is null))
{
    ShowUsage();
    return;
}

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
            "issue",
            test)));
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
            "pull request",
            test)));
    }
}

await Task.WhenAll(tasks);

async Task ProcessPrediction<T>(string modelPath, ulong number, Func<Task<T>> getItem, Func<string, bool> labelPredicate, string itemType, bool test) where T : Issue
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
