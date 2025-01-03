using Microsoft.ML;
using Microsoft.ML.AutoML;
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
    Console.WriteLine("Inferring columns from data...");
    var mlContext = new MLContext();
    // var columns = mlContext.Auto().InferColumns(
    //     issueDataPath,
    //     labelColumnName: "Label",
    //     separatorChar: '\t',
    //     groupColumns: false);


    Console.WriteLine("Loading data...");
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
            new("Label", DataKind.String, 1),
            new("Title", DataKind.String, 2),
            new("Body", DataKind.String, 3)
        ]
    };

    var loader = mlContext.Data.CreateTextLoader(textLoaderOptions);
    var data = loader.Load(issueDataPath);
    var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.1);

    Console.WriteLine("Constructing pipeline...");
    ColumnInformation columnInfo = new() { LabelColumnName = "Label" };
    columnInfo.TextColumnNames.Add("Title");
    columnInfo.TextColumnNames.Add("Body");

    var pipeline = mlContext.Auto()
        .Featurizer(data, columnInformation: columnInfo)
        .Append(mlContext.Auto().MultiClassification());

    mlContext.Log += (_, e) => {
        if (e.Source.StartsWith("AutoMLExperiment") && e.Message.Contains("Elapsed"))
        {
            Console.WriteLine(e.Message);
        }
    };

    Console.WriteLine("Running experiment...");
    var settings = new MulticlassExperimentSettings();
    settings.Trainers.Clear();
    settings.Trainers.Add(MulticlassClassificationTrainer.SdcaMaximumEntropy);
    settings.MaxExperimentTimeInSeconds = 300;

    var xf = mlContext.Transforms;
    var preFeaturizer =
        xf.Text.FeaturizeText("TextFeatures", new TextFeaturizingEstimator.Options(), [ "Title", "Body" ])
        .Append(xf.FeatureSelection.SelectFeaturesBasedOnCount("TextFeatures", "TextFeatures", 2))
        .AppendCacheCheckpoint(mlContext);

    var experiment = mlContext.Auto().CreateMulticlassClassificationExperiment(settings);
    var result = experiment.Execute(
        split.TrainSet,
        split.TestSet,
        preFeaturizer: preFeaturizer);

    Console.WriteLine("Saving model...");
    mlContext.Model.Save(result.BestRun.Model, split.TrainSet.Schema, issueModelPath);
}
