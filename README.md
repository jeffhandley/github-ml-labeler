# github-ml-labeler
Use a machine learning model to automatically label issues and pull requests.

## Downloader

Download issue and pull request data from GitHub, creating tab-separated (.tsv) data files to be consumed by the Trainer.

## Trainer

Load the tab-separated issue and pull request data that has already been downloaded, and train an ML.NET model over the data to prepare for making label predictions.

## Predictor

Consume the ML.NET model and make predictions for issues and pull requests.

## Tester

Perform a comparison test run over GitHub data, predicting labels and comparing the predictions against the actual values. This can be performed either by downloading issue and pull request data from GitHub or loading a tab-separated (.tsv) file created by the Downloader.

## Reusable GitHub Workflows

The `.github/workflows` folder exposes reusable workflows that can be used from other repositories to integrate automated labeling for issues and pull requests.

### `download-issues.yml` / `download-pulls.yml`

Invokes the Downloader, saving the `.tsv` file to the Actions cache withing the calling repository. Supports storing multiple data files in cache side-by-side using cache key suffixes, which enables building and testing new models without disrupting predictions.

### `model-issues.yml` / `model-pulls.yml`

Invokes the Trainer, consuming a `.tsv` data file from the Actions cache to build a model. The model is persisted into the Actions cache. Supports storing multiple models in cache side-by-side using cache key suffixes, which enables testing and staging new models without disrupting predictions.

### `test-issues.yml` / `test-pulls.yml`

Invokes the Tester, consuming an ML.NET model from the Actions cache to test against actual issue/pull labels.

### `promote-issues.yml` / `promote-pulls.yml`

Promotes a model persisted to cache with a cache key suffix to use the default cache key, thus promoting it into the production cache slot for predictions.

### `predict-issue.yml` / `predict-pull.yml`

Invokes the Predictor, predicting and applying labels to issues and pull requests. This can be called with either a single issue/pull number, or a comma-separated list of number ranges (e.g. `1-1000,2000-3000`). Supports specifying a cache key suffix for making predictions from a model in a test/staging slot.
