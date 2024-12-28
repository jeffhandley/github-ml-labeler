using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
using Microsoft.ML.Transforms.Text;

void ShowUsage(string? message = null)
{
    Console.WriteLine($"Invalid or missing arguments.{(message is null ? "" : " " + message)}");
    Console.WriteLine("  [--issue-data {path/to/issue-data.tsv} --issue-model {path/to/issue-model.zip}]");
    Console.WriteLine("  [--pull-data {path/to/pull-data.tsv} --pull-model {path/to/pull-model.zip}]");

    Environment.Exit(-1);
}

Queue<string> arguments = new(args);
string? issueDataPath = null;
string? issueModelPath = null;
string? pullDataPath = null;
string? pullModelPath = null;

while (arguments.Count > 0)
{
    string argument = arguments.Dequeue();

    switch (argument)
    {
        case "--issue-data":
            issueDataPath = arguments.Dequeue();
            break;
        case "--issue-model":
            issueModelPath = arguments.Dequeue();
            break;
        case "--pull-data":
            pullDataPath = arguments.Dequeue();
            break;
        case "--pull-model":
            pullModelPath = arguments.Dequeue();
            break;
        default:
            ShowUsage($"Unrecognized argument: {argument}");
            return;
    }
}

if ((issueDataPath is null != issueModelPath is null) ||
    (pullDataPath is null != pullModelPath is null) ||
    (issueModelPath is null && pullModelPath is null))
{
    ShowUsage();
    return;
}

if (issueDataPath is not null && issueModelPath is not null)
{
    var issueContext = new MLContext();
    var issueColumns = issueContext.Auto().InferColumns(
        issueDataPath,
        separatorChar: '\t',
        labelColumnIndex: 1,
        hasHeader: true);
    var issueLoader = issueContext.Data.CreateTextLoader(issueColumns.TextLoaderOptions);
    var issueData = issueLoader.Load(issueDataPath);
    var issueDataSets = issueContext.Data.TrainTestSplit(issueData, testFraction: 0.2);
    var issueTrainer = issueContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey", "Features");
    var issuePipeline = issueContext
        .Transforms.Conversion.MapValueToKey(outputColumnName: "LabelKey", inputColumnName: "Label")
        .Append(issueContext.Transforms.Text.FeaturizeText(outputColumnName: "TitleFeature", inputColumnName: "Title"))
        .Append(issueContext.Transforms.Text.FeaturizeText(outputColumnName: "BodyFeature", inputColumnName: "Body"))
        .Append(issueContext.Transforms.Concatenate(outputColumnName: "Features", "TitleFeature", "BodyFeature"))
        .Append(issueTrainer)
        .Append(issueContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

    issueContext.MulticlassClassification.CrossValidate(
        data: issueDataSets.TrainSet,
        estimator: issuePipeline,
        numberOfFolds: 6,
        labelColumnName: "LabelKey");

    var issueModel = issuePipeline.Fit(issueDataSets.TrainSet);
    issueContext.Model.Save(issueModel, issueDataSets.TrainSet.Schema, issueModelPath);

    var testIssue = new Issue
    {
        Number = 34,
        Title = "Bug with List<T>",
        Body = "I have encountered a bug when using List<T>. The collection does not work like I expected."
    };

    var engine = issueContext.Model.CreatePredictionEngine<Issue, LabelPrediction>(issueModel);
    var prediction = engine.Predict(testIssue);
    Console.WriteLine($"Test Issue:\n  Number: {testIssue.Number}\n  Title: {testIssue.Title}\n  Body: {testIssue.Body}\n  PREDICTED LABEL: {prediction.PredictedLabel}");
}

if (pullDataPath is not null && pullModelPath is not null)
{
    var pullContext = new MLContext();
    var pullColumns = pullContext.Auto().InferColumns(
        pullDataPath,
        separatorChar: '\t',
        labelColumnIndex: 1,
        hasHeader: true);
    var pullLoader = pullContext.Data.CreateTextLoader(pullColumns.TextLoaderOptions);
    var pullData = pullLoader.Load(pullDataPath);
    var pullDataSets = pullContext.Data.TrainTestSplit(pullData, testFraction: 0.2);
    var pullTrainer = pullContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey", "Features");
    var pullPipeline = pullContext
        .Transforms.Conversion.MapValueToKey(outputColumnName: "LabelKey", inputColumnName: "Label")
        .Append(pullContext.Transforms.Text.FeaturizeText(outputColumnName: "TitleFeature", inputColumnName: "Title"))
        .Append(pullContext.Transforms.Text.FeaturizeText(outputColumnName: "BodyFeature", inputColumnName: "Body"))
        .Append(pullContext.Transforms.Text.FeaturizeText(outputColumnName: "FileNamesFeature", inputColumnName: "FileNames"))
        .Append(pullContext.Transforms.Text.FeaturizeText(outputColumnName: "FolderNamesFeature", inputColumnName: "FolderNames"))
        .Append(pullContext.Transforms.Concatenate(outputColumnName: "Features", "TitleFeature", "BodyFeature", "FileNamesFeature", "FolderNamesFeature"))
        .Append(pullTrainer)
        .Append(pullContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

    pullContext.MulticlassClassification.CrossValidate(
        data: pullDataSets.TrainSet,
        estimator: pullPipeline,
        numberOfFolds: 6,
        labelColumnName: "LabelKey");

    var pullModel = pullPipeline.Fit(pullDataSets.TrainSet);
    pullContext.Model.Save(pullModel, pullDataSets.TrainSet.Schema, pullModelPath);

    var testPull = new PullRequest
    {
        Number = 42,
        Title = "Fix bug with List<T>",
        Body = "Fixes #34",
        FileNames = "List.cs List List.Generic.Tests.cs List.Generic.Tests",
        FolderNames = "src src/libraries src/libraries/System.Collections src/libraries/System.Collections/tests src/libraries/System.Collections/tests/Generic src/libraries/System.Collections/tests/Generic/List src src/libraries src/libraries/System.Private.CoreLib src/libraries/System.Private.CoreLib/src src/libraries/System.Private.CoreLib/src/System src/libraries/System.Private.CoreLib/src/System/Collections src/libraries/System.Private.CoreLib/src/System/Collections/Generic"
    };

    var engine = pullContext.Model.CreatePredictionEngine<PullRequest, LabelPrediction>(pullModel);
    var prediction = engine.Predict(testPull);
    Console.WriteLine($"Test Pull:\n  Number: {testPull.Number}\n  Title: {testPull.Title}\n  Body: {testPull.Body}\n  FileNames: {testPull.FileNames}\n  {testPull.FolderNames}\n  PREDICTED LABEL: {prediction.PredictedLabel}");
}
