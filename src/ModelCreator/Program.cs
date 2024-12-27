global using Microsoft.ML.Data;

using Microsoft.ML;
using Microsoft.ML.AutoML;
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
    var columnInference = mlContext.Auto().InferColumns(
        issueData,
        separatorChar: '\t',
        labelColumnIndex: 1,
        hasHeader: true);
    var loader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);
    var data = loader.Load(issueData);
    var trainTestData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
    var trainer = mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey", "Features");
    var pipeline = mlContext
        .Transforms.Conversion.MapValueToKey(outputColumnName: "LabelKey", inputColumnName: "Label")
        .Append(mlContext.Transforms.Text.FeaturizeText(outputColumnName: "TitleFeature", inputColumnName: "Title"))
        .Append(mlContext.Transforms.Text.FeaturizeText(outputColumnName: "BodyFeature", inputColumnName: "Body"))
        .Append(mlContext.Transforms.Concatenate(outputColumnName: "Features", "TitleFeature", "BodyFeature"))
        .Append(trainer)
        .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

    var crossValidation = mlContext.MulticlassClassification.CrossValidate(
        data: trainTestData.TrainSet,
        estimator: pipeline,
        numberOfFolds: 6,
        labelColumnName: "LabelKey");

    var trainedModel = pipeline.Fit(trainTestData.TrainSet);
    mlContext.Model.Save(trainedModel, trainTestData.TrainSet.Schema, issueModel);

    var testIssue = new Issue
    {
        Number = 42,
        Title = "Bug with List<T>",
        Body = "I have encountered a bug when using List<T>. The collection does not work like I expected."
    };

    var engine = mlContext.Model.CreatePredictionEngine<Issue, LabelPrediction>(trainedModel);
    var prediction = engine.Predict(testIssue);
    Console.WriteLine($"Test Issue:\n  Number: {testIssue.Number}\n  Title: {testIssue.Title}\n  Body: {testIssue.Body}\n  LABEL: {prediction.PredictedLabel}");
}
