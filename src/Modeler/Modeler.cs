using static DataFileUtils;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
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
    Console.WriteLine("Inferring model schema from data...");
    var context = new MLContext();
    var columns = context.Auto().InferColumns(
        dataPath,
        separatorChar: '\t',
        labelColumnIndex: 0,
        hasHeader: true
    );

    var textColumns = columns.ColumnInformation.TextColumnNames;

    if (!textColumns.Contains("Title") || !textColumns.Contains("Body"))
    {
        throw new ApplicationException("Model loading failed; Title and Body columns were not inferred in the model. It's likely the data set is too small.");
    }

    if (type == ModelType.PullRequest && (!textColumns.Contains("FileNames") || !textColumns.Contains("FolderNames")))
    {
        throw new ApplicationException("Model loading failed; FileNames and FolderNames columns were not inferred in the model. It's likely the data set is too small.");
    }

    Console.WriteLine("Loading data into model...");
    var loader = context.Data.CreateTextLoader(columns.TextLoaderOptions);
    var data = loader.Load(dataPath);

    Console.WriteLine("Splitting data into train and test data sets...");
    var dataSplit = context.Data.TrainTestSplit(data, testFraction: 0.2);

    Console.WriteLine("Constructing pipeline...");
    var trainer = context.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey", "Features");
    var featurizedPipeline = context
        .Transforms.Conversion.MapValueToKey(outputColumnName: "LabelKey", inputColumnName: "Label")
        .Append(context.Transforms.Text.FeaturizeText(outputColumnName: "TitleFeature", inputColumnName: "Title"))
        .Append(context.Transforms.Text.FeaturizeText(outputColumnName: "BodyFeature", inputColumnName: "Body"));

    string[] featurizedColumns = ["TitleFeature", "BodyFeature"];

    if (type == ModelType.PullRequest)
    {
        featurizedPipeline = featurizedPipeline
        .Append(context.Transforms.Text.FeaturizeText(outputColumnName: "FileNamesFeature", inputColumnName: "FileNames"))
        .Append(context.Transforms.Text.FeaturizeText(outputColumnName: "FolderNamesFeature", inputColumnName: "FolderNames"));

        featurizedColumns = ["TitleFeature", "BodyFeature", "FileNamesFeature", "FolderNamesFeature"];
    }

    var pipeline = featurizedPipeline
        .Append(context.Transforms.Concatenate(outputColumnName: "Features", featurizedColumns))
        .Append(trainer)
        .Append(context.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

    Console.WriteLine("Cross-validating model...");
    context.MulticlassClassification.CrossValidate(
        data: dataSplit.TrainSet,
        estimator: pipeline,
        numberOfFolds: 6,
        labelColumnName: "LabelKey");

    Console.WriteLine("Fitting model...");
    var model = pipeline.Fit(dataSplit.TrainSet);

    Console.WriteLine($"Saving model to {modelPath}...");
    EnsureOutputDirectory(modelPath);
    context.Model.Save(model, dataSplit.TrainSet.Schema, modelPath);

    Console.WriteLine("Model successfully saved.");
}
