using static DataFileUtils;
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
    CreateModel(issueDataPath, issueModelPath, ModelType.Issue);
}

if (pullDataPath is not null && pullModelPath is not null)
{
    CreateModel(pullDataPath, pullModelPath, ModelType.PullRequest);
}

static void CreateModel(string dataPath, string modelPath, ModelType type)
{
    Console.WriteLine("Inferring columns from data...");
    var mlContext = new MLContext();
    var columns = mlContext.Auto().InferColumns(
        dataPath,
        labelColumnName: "Label",
        separatorChar: '\t',
        groupColumns: false);

    Console.WriteLine("Loading data...");
    var loader = mlContext.Data.CreateTextLoader(columns.TextLoaderOptions);
    var data = loader.Load(dataPath);
    var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.1);

    Console.WriteLine("Constructing pipeline...");
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
    settings.MaxExperimentTimeInSeconds = 60 * 5;

    string[] featurizedColumns = type == ModelType.Issue ?
        [ "Title", "Body" ] :
        [ "Title", "Body", "FileNames", "FolderNames" ];

    var xf = mlContext.Transforms;
    var preFeaturizer =
        xf.Text.FeaturizeText("TextFeatures", new TextFeaturizingEstimator.Options(), featurizedColumns)
        .Append(xf.FeatureSelection.SelectFeaturesBasedOnCount("TextFeatures", count: 2))
        .AppendCacheCheckpoint(mlContext);

    var experiment = mlContext.Auto().CreateMulticlassClassificationExperiment(settings);
    var result = experiment.Execute(
        split.TrainSet,
        split.TestSet,
        preFeaturizer: preFeaturizer);

    Console.WriteLine("Fitting model...");
    result.BestRun.Estimator.Fit(data);

    Console.WriteLine("Saving model...");
    EnsureOutputDirectory(modelPath);
    mlContext.Model.Save(result.BestRun.Model, split.TrainSet.Schema, modelPath);
}
