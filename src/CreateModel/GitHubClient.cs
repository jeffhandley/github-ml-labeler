using System.Text.Json;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GitHubModel;

public partial class GitHubClient
{
    public static GraphQLHttpClient CreateGraphQLClient(string githubToken)
    {
        GraphQLHttpClient client = new GraphQLHttpClient(
            "https://api.github.com/graphql",
            new SystemTextJsonSerializer()
        );

        client.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                scheme: "bearer",
                parameter: githubToken);

        client.HttpClient.Timeout = TimeSpan.FromMinutes(2);

        return client;
    }

    public static async IAsyncEnumerable<(Issue Issue, string Label)> DownloadIssues(string githubToken, string org, string repo, int pageLimit = 1000)
    {
        await foreach (var item in DownloadItems<Issue>(githubToken, org, repo, pageLimit, "issues"))
        {
            yield return (item.Item, item.Label);
        }
    }

    public static async IAsyncEnumerable<(PullRequest PullRequest, string Label)> DownloadPullRequests(string githubToken, string org, string repo, int pageLimit = 1000)
    {
        var items = DownloadItems<PullRequest>(githubToken, org, repo, pageLimit, "pullRequests");

        await foreach (var item in items)
        {
            yield return (item.Item, item.Label);
        }
    }

    private static async IAsyncEnumerable<(T Item, string Label)> DownloadItems<T>(string githubToken, string org, string repo, int pageLimit, string itemQueryName) where T : Issue
    {
        int pageNumber = 1;
        string? after = null;
        int loadedCount = 0;
        int? totalCount = null;
        byte? retries = 0;

        const byte retryLimit = 5;

        do
        {
            Console.WriteLine($"Downloading {itemQueryName} page {pageNumber} of {pageLimit}...{(retries > 0 ? $"Retry {retries} of {retryLimit}" : "")}");

            Page<T> page;

            try
            {
                page = await GetItemsPage<T>(githubToken, org, repo, after, itemQueryName);
            }
            catch (Exception ex) when (ex is HttpIOException || ex is GraphQLHttpRequestException)
            {
                retries++;
                continue;
            }

            // Abort if paging did not progress
            if (after == page.EndCursor) break;

            pageNumber++;
            after = page.EndCursor;
            loadedCount += page.Nodes.Length;
            totalCount ??= page.TotalCount;
            retries = 0;

            Console.WriteLine($"Total {itemQueryName}: {loadedCount} of {totalCount}. Cursor: '{after}'.");

            foreach (T item in page.Nodes)
            {
                // If there are more labels, there might be other applicable
                // labels that were not loaded and the model is incomplete.
                if (item.Labels.HasNextPage) continue;

                // Only items with exactly one applicable label are used for the model.
                string[] labels = Array.FindAll(item.LabelNames, label => label.StartsWith("area-"));
                if (labels.Length != 1) continue;

                // Exactly one applicable label was found on the item. Include it in the model.
                Console.WriteLine($"{itemQueryName} {org}/{repo}#{item.Number} {labels[0]}");

                yield return (item, labels[0]);
            }
        }
        while (after is not null && pageNumber <= pageLimit && retries <= retryLimit);
    }

    private static async Task<Page<T>> GetItemsPage<T>(string githubToken, string org, string repo, string? after, string itemQueryName) where T : Issue
    {
        using GraphQLHttpClient client = CreateGraphQLClient(githubToken);

        string files = typeof(T) == typeof(PullRequest) ? "files (first: 100) { nodes { path } }" : "";

        GraphQLRequest query = new GraphQLRequest
        {
            Query = $$"""
                query Issues ($owner: String!, $repo: String!, $after: String) {
                    repository(owner: $owner, name: $repo) {
                        items:{{itemQueryName}} (after: $after, first: 100, orderBy: {field: CREATED_AT, direction: DESC}) {
                            nodes {
                                number
                                title
                                bodyText
                                labels(first: 25) {
                                    nodes { name },
                                    pageInfo { hasNextPage }
                                }
                                {{files}}
                            }
                            pageInfo {
                                hasNextPage
                                endCursor
                            }
                            totalCount
                        }
                    }
                }
                """,
            Variables = new
            {
                Owner = org,
                Repo = repo,
                After = after
            }
        };

        return (await client.SendQueryAsync<RepositoryQuery<T>>(query)).Data.Repository.Items;
    }
}
