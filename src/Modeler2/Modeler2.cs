using static DataFileUtils;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

(
    string? issueDataPath,
    string? issueModelPath,
    string? pullDataPath,
    string? pullModelPath
) = Args.Parse(args);

if (issueDataPath is not null && issueModelPath is not null)
{
    CreateModel(issueDataPath, issueModelPath, ModelType.Issue);
}

if (pullDataPath is not null && pullModelPath is not null)
{
    CreateModel(pullDataPath, pullModelPath, ModelType.PullRequest);
}

static void CreateModel(string dataPath, string modelPath, ModelType type)
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
        Columns = type == ModelType.Issue ? [
            new("Number", DataKind.Single, 0),
            new("Label", DataKind.String, 1),
            new("Title", DataKind.String, 2),
            new("Body", DataKind.String, 3),
        ] : [
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
    var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.1);

    Console.WriteLine("Building pipeline...");
    var xf = mlContext.Transforms;
    var featurizedPipeline = xf.Conversion.MapValueToKey(inputColumnName: "Label", outputColumnName: "LabelKey")
        .Append(xf.Text.FeaturizeText(inputColumnName: "Title", outputColumnName: "TitleFeature"))
        .Append(xf.Text.FeaturizeText(inputColumnName: "Body", outputColumnName: "BodyFeature"));

    string[] featurizedColumns = ["TitleFeature", "BodyFeature"];

    if (type == ModelType.PullRequest)
    {
        featurizedPipeline = featurizedPipeline
        .Append(xf.Text.FeaturizeText(inputColumnName: "FileNames", outputColumnName: "FileNamesFeature"))
        .Append(xf.Text.FeaturizeText(inputColumnName: "FolderNames", outputColumnName: "FolderNamesFeature"));

        featurizedColumns = ["TitleFeature", "BodyFeature", "FileNamesFeature", "FolderNamesFeature"];
    }

    var pipeline = featurizedPipeline
        .Append(xf.Concatenate("Features", featurizedColumns))
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
    EnsureOutputDirectory(modelPath);
    mlContext.Model.Save(trainedModel, split.TrainSet.Schema, modelPath);
}
