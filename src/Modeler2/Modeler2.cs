using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

(
    string? issueDataPath,
    string? issueModelPath,
    string? pullDataPath,
    string? pullModelPath
) = Args.Parse(args);

// var experimentSettings = new MulticlassExperimentSettings();
// experimentSettings.Trainers.Clear();
// experimentSettings.Trainers.Add(MulticlassClassificationTrainer.SdcaMaximumEntropy);
// experimentSettings.CacheDirectoryName = Path.GetTempPath();
// experimentSettings.OptimizingMetric = MulticlassClassificationMetric.MicroAccuracy;
// experimentSettings.MaxExperimentTimeInSeconds = 60 * 2;

// ColumnInformation columnInfo = new() { LabelColumnName = "Label" };
// columnInfo.NumericColumnNames.Add("Number");
// columnInfo.TextColumnNames.Add("Title");
// columnInfo.TextColumnNames.Add("Body");

// var columnInference = mlContext.Auto().InferColumns(args[0], "Label", groupColumns: false);
// var textLoaderOptions = columnInference.TextLoaderOptions;
// var columnInfo = columnInference.ColumnInformation;

if (issueDataPath is not null && issueModelPath is not null)
{
    TrainIssues(issueDataPath, issueModelPath);
}

if (pullDataPath is not null && pullModelPath is not null)
{
    TrainPulls(pullDataPath, pullModelPath);
}

void TrainIssues(string dataPath, string modelPath)
{
    Console.WriteLine("Loading issue data into train/test sets...");
    MLContext mlContext = new();

    TextLoader.Options textLoaderOptions = new()
    {
        AllowQuoting = true,
        AllowSparse = false,
        EscapeChar = '"',
        HasHeader = true,
        ReadMultilines = false,
        Separators = ['\t'],
        TrimWhitespace = true,
        UseThreads = true,
        Columns = [
            new("Label", DataKind.String, 0),
            new("Title", DataKind.String, 1),
            new("Body", DataKind.String, 2)
        ]
    };

    var loader = mlContext.Data.CreateTextLoader(textLoaderOptions);
    var data = loader.Load(dataPath);

    // var data = mlContext.Data.LoadFromTextFile<Issue>(args[0], hasHeader: true, separatorChar: '\t');
    var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.1);

    Console.WriteLine("Building pipeline...");
    var xf = mlContext.Transforms;
    var pipeline = xf.Conversion.MapValueToKey(inputColumnName: "Label", outputColumnName: "LabelKey")
        .Append(xf.Text.FeaturizeText(inputColumnName: "Title", outputColumnName: "TitleFeature"))
        .Append(xf.Text.FeaturizeText(inputColumnName: "Body", outputColumnName: "BodyFeature"))
        .Append(xf.Concatenate("Features", ["TitleFeature", "BodyFeature"]))
        .AppendCacheCheckpoint(mlContext)
        .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey"))
        .Append(xf.Conversion.MapKeyToValue("PredictedLabel"));

    Console.WriteLine("Fitting train set...");
    var trainedModel = pipeline.Fit(split.TrainSet);
    var testModel = trainedModel.Transform(split.TestSet);

    Console.WriteLine("Evaluating test set...");
    var metrics = mlContext.MulticlassClassification.Evaluate(testModel, labelColumnName: "LabelKey");

    Console.WriteLine($"************************************************************");
    Console.WriteLine($"MacroAccuracy = {metrics.MacroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
    Console.WriteLine($"MicroAccuracy = {metrics.MicroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
    Console.WriteLine($"LogLoss = {metrics.LogLoss:0.####}, the closer to 0, the better");
    Console.WriteLine($"LogLoss for class 1 = {metrics.PerClassLogLoss[0]:0.####}, the closer to 0, the better");
    Console.WriteLine($"LogLoss for class 2 = {metrics.PerClassLogLoss[1]:0.####}, the closer to 0, the better");
    Console.WriteLine($"LogLoss for class 3 = {metrics.PerClassLogLoss[2]:0.####}, the closer to 0, the better");
    Console.WriteLine($"************************************************************");

    Console.WriteLine("Saving model...");
    mlContext.Model.Save(trainedModel, split.TrainSet.Schema, modelPath);
}

void TrainPulls(string dataPath, string modelPath)
{
    Console.WriteLine("Loading data into train/test sets...");
    MLContext mlContext = new();

    TextLoader.Options textLoaderOptions = new()
    {
        AllowQuoting = true,
        AllowSparse = false,
        EscapeChar = '"',
        HasHeader = true,
        ReadMultilines = false,
        Separators = ['\t'],
        TrimWhitespace = true,
        UseThreads = true,
        Columns = [
            new("Number", DataKind.Single, 0),
            new("Label", DataKind.String, 1),
            new("Title", DataKind.String, 2),
            new("Body", DataKind.String, 3),
            new("FileNames", DataKind.String, 4),
            new("FolderNames", DataKind.String, 5)
        ]
    };

    var loader = mlContext.Data.CreateTextLoader(textLoaderOptions);
    var data = loader.Load(dataPath);

    // var data = mlContext.Data.LoadFromTextFile<Issue>(args[0], hasHeader: true, separatorChar: '\t');
    var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.1);

    Console.WriteLine("Building pipeline...");
    var xf = mlContext.Transforms;
    var pipeline = xf.Conversion.MapValueToKey(inputColumnName: "Label", outputColumnName: "LabelKey")
        .Append(xf.Text.FeaturizeText(inputColumnName: "Title", outputColumnName: "TitleFeature"))
        .Append(xf.Text.FeaturizeText(inputColumnName: "Body", outputColumnName: "BodyFeature"))
        .Append(xf.Text.FeaturizeText(inputColumnName: "FileNames", outputColumnName: "FileNamesFeature"))
        .Append(xf.Text.FeaturizeText(inputColumnName: "FolderNames", outputColumnName: "FolderNamesFeature"))
        .Append(xf.Concatenate("Features", ["TitleFeature", "BodyFeature", "FileNamesFeature", "FolderNamesFeature"]))
        .Append(xf.Concatenate("Features", ["TitleFeature", "BodyFeature"]))
        .AppendCacheCheckpoint(mlContext)
        .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey"))
        .Append(xf.Conversion.MapKeyToValue("PredictedLabel"));

    Console.WriteLine("Fitting train set...");
    var trainedModel = pipeline.Fit(split.TrainSet);
    var testModel = trainedModel.Transform(split.TestSet);

    Console.WriteLine("Evaluating test set...");
    var metrics = mlContext.MulticlassClassification.Evaluate(testModel, labelColumnName: "LabelKey");

    Console.WriteLine($"************************************************************");
    Console.WriteLine($"MacroAccuracy = {metrics.MacroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
    Console.WriteLine($"MicroAccuracy = {metrics.MicroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
    Console.WriteLine($"LogLoss = {metrics.LogLoss:0.####}, the closer to 0, the better");
    Console.WriteLine($"LogLoss for class 1 = {metrics.PerClassLogLoss[0]:0.####}, the closer to 0, the better");
    Console.WriteLine($"LogLoss for class 2 = {metrics.PerClassLogLoss[1]:0.####}, the closer to 0, the better");
    Console.WriteLine($"LogLoss for class 3 = {metrics.PerClassLogLoss[2]:0.####}, the closer to 0, the better");
    Console.WriteLine($"************************************************************");

    Console.WriteLine("Saving model...");
    mlContext.Model.Save(trainedModel, split.TrainSet.Schema, modelPath);
}

// // Console.WriteLine("Running experiment...");

// // var experimentResult = mlContext.Auto()
// //     .CreateMulticlassClassificationExperiment(experimentSettings)
// //     .Execute(
// //         split.TrainSet,
// //         columnInfo,
// //         preFeaturizer: preFeaturizer,
// //         progressHandler: new MulticlassExperimentProgressHandler()
// //     );

// // Console.WriteLine("Saving model...");

// // mlContext.Model.Save(experimentResult.BestRun.Model, split.TrainSet.Schema, args[1]);

// #pragma warning disable CS0649
// class Issue
// {
//     [LoadColumn(1)]
//     public string? Label;
//     [LoadColumn(2)]
//     public required string Title;
//     [LoadColumn(3)]
//     public string? Body;
// }

// class IssuePrediction : Issue
// {
//     public required string PredictedLabel;
//     public required float Score;
// }
// #pragma warning restore CS0649
