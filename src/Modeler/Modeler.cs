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
    Console.WriteLine("Inferring model schema from data...");
    var issueContext = new MLContext();
    var issueColumns = issueContext.Auto().InferColumns(
        issueDataPath,
        separatorChar: '\t',
        labelColumnIndex: 1,
        hasHeader: true);

    var textColumns = issueColumns.ColumnInformation.TextColumnNames;

    if (!textColumns.Contains("Title") || !textColumns.Contains("Body"))
    {
        throw new ApplicationException("Model loading failed; Title and Body columns were not inferred in the model. It's likely the data set is too small.");
    }

    Console.WriteLine("Loading data into model...");
    var issueLoader = issueContext.Data.CreateTextLoader(issueColumns.TextLoaderOptions);
    var issueData = issueLoader.Load(issueDataPath);

    Console.WriteLine("Splitting data into train and test data sets...");
    var issueDataSets = issueContext.Data.TrainTestSplit(issueData, testFraction: 0.2);

    Console.WriteLine("Constructing pipeline...");
    var issueTrainer = issueContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey", "Features");
    var issuePipeline = issueContext
        .Transforms.Conversion.MapValueToKey(outputColumnName: "LabelKey", inputColumnName: "Label")
        .Append(issueContext.Transforms.Text.FeaturizeText(outputColumnName: "TitleFeature", inputColumnName: "Title"))
        .Append(issueContext.Transforms.Text.FeaturizeText(outputColumnName: "BodyFeature", inputColumnName: "Body"))
        .Append(issueContext.Transforms.Concatenate(outputColumnName: "Features", "TitleFeature", "BodyFeature"))
        .Append(issueTrainer)
        .Append(issueContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

    Console.WriteLine("Cross-validating model...");
    issueContext.MulticlassClassification.CrossValidate(
        data: issueDataSets.TrainSet,
        estimator: issuePipeline,
        numberOfFolds: 6,
        labelColumnName: "LabelKey");

    Console.WriteLine("Fitting model...");
    var issueModel = issuePipeline.Fit(issueDataSets.TrainSet);

    Console.WriteLine($"Saving model to {issueModelPath}...");
    issueContext.Model.Save(issueModel, issueDataSets.TrainSet.Schema, issueModelPath);

    Console.WriteLine("Model successfully saved.");
}

if (pullDataPath is not null && pullModelPath is not null)
{
    Console.WriteLine("Inferring model schema from data...");
    var pullContext = new MLContext();
    var pullColumns = pullContext.Auto().InferColumns(
        pullDataPath,
        separatorChar: '\t',
        labelColumnIndex: 1,
        hasHeader: true);

    var textColumns = pullColumns.ColumnInformation.TextColumnNames;

    if (!textColumns.Contains("Title") || !textColumns.Contains("Body"))
    {
        throw new ApplicationException("Model loading failed; Title and Body columns were not inferred in the model. It's likely the data set is too small.");
    }

    Console.WriteLine("Loading data into model...");
    var pullLoader = pullContext.Data.CreateTextLoader(pullColumns.TextLoaderOptions);
    var pullData = pullLoader.Load(pullDataPath);

    Console.WriteLine("Splitting data into train and test data sets...");
    var pullDataSets = pullContext.Data.TrainTestSplit(pullData, testFraction: 0.2);

    Console.WriteLine("Constructing pipeline...");
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

    Console.WriteLine("Cross-validating model...");
    pullContext.MulticlassClassification.CrossValidate(
        data: pullDataSets.TrainSet,
        estimator: pullPipeline,
        numberOfFolds: 6,
        labelColumnName: "LabelKey");

    Console.WriteLine("Fitting model...");
    var pullModel = pullPipeline.Fit(pullDataSets.TrainSet);

    Console.WriteLine($"Saving model to {issueModelPath}...");
    pullContext.Model.Save(pullModel, pullDataSets.TrainSet.Schema, pullModelPath);

    Console.WriteLine("Model successfully saved.");
}
