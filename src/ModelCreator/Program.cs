global using Microsoft.ML.Data;

using Microsoft.ML;
using Microsoft.ML.Transforms;
using Microsoft.ML.Transforms.Text;

void ShowUsage()
{
    Console.WriteLine("Expected: [--issue-data {path/to/issue-data.tsv} --issue-model {path/to/issue-model.zip}] [--pull-data {path/to/pull-data.tsv} --pull-model {path/to/pull-model.zip}]");
    Environment.Exit(-1);
}

if (args.Length < 2)
{
    ShowUsage();
    return;
}

Queue<string> arguments = new(args);

string? issueData = null;
string? issueModel = null;
string? pullData = null;
string? pullModel = null;

while (arguments.Count > 1)
{
    string option = arguments.Dequeue();

    switch (option)
    {
        case "--issue-data":
            issueData = arguments.Dequeue();
            break;
        case "--issue-model":
            issueModel = arguments.Dequeue();
            break;
        case "--pull-data":
            pullData = arguments.Dequeue();
            break;
        case "--pull-model":
            pullModel = arguments.Dequeue();
            break;
        default:
            ShowUsage();
            return;
    }
}

if (arguments.Count == 1)
{
    ShowUsage();
    return;
}

if ((issueData is not null && issueModel is null) || (issueData is null && issueModel is not null))
{
    ShowUsage();
    return;
}

if (issueData is not null && issueModel is not null)
{
    var mlContext = new MLContext();
    var data = mlContext.Data.LoadFromTextFile<Issue>(issueData, separatorChar: '\t', hasHeader: false);

    // Split data into train and test sets
    var splitData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
    var trainData = splitData.TrainSet;
    var testData = splitData.TestSet;
}
