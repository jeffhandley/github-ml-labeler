global using Microsoft.ML.Data;

using Microsoft.ML;
using Microsoft.ML.Transforms;
using Microsoft.ML.Transforms.Text;

void ShowUsage()
{
    Console.WriteLine("Expected: [-i {path/to/issues.tsv}] [-p {path/to/pulls.tsv}]");
    Environment.Exit(-1);
}

if (args.Length < 2)
{
    ShowUsage();
    return;
}

Queue<string> arguments = new(args);

string? issuesPath = null;
string? pullsPath = null;

while (arguments.Count > 1)
{
    string option = arguments.Dequeue();

    switch (option)
    {
        case "-i":
            issuesPath = arguments.Dequeue();
            break;
        case "-p":
            pullsPath = arguments.Dequeue();
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

if (issuesPath is not null)
{
    var mlContext = new MLContext();
    var data = mlContext.Data.LoadFromTextFile<Issue>(issuesPath, separatorChar: '\t', hasHeader: false);

    // Split data into train and test sets
    var splitData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
    var trainData = splitData.TrainSet;
    var testData = splitData.TestSet;
}
