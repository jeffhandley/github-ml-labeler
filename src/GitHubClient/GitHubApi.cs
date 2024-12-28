using System.Net.Http.Json;
using System.Text;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace GitHubClient;

public class GitHubApi
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

        return client;
    }

    public static async IAsyncEnumerable<(Issue Issue, string Label)> DownloadIssues(string githubToken, string org, string repo, Predicate<string> labelPredicate, int pageLimit, int[] retries, bool verbose)
    {
        await foreach (var item in DownloadItems<Issue>("issues", githubToken, org, repo, labelPredicate, pageLimit, retries, verbose))
        {
            yield return (item.Item, item.Label);
        }
    }

    public static async IAsyncEnumerable<(PullRequest PullRequest, string Label)> DownloadPullRequests(string githubToken, string org, string repo, Predicate<string> labelPredicate, int pageLimit, int[] retries, bool verbose)
    {
        var items = DownloadItems<PullRequest>("pullRequests", githubToken, org, repo, labelPredicate, pageLimit, retries, verbose);

        await foreach (var item in items)
        {
            yield return (item.Item, item.Label);
        }
    }

    private static async IAsyncEnumerable<(T Item, string Label)> DownloadItems<T>(string itemQueryName, string githubToken, string org, string repo, Predicate<string> labelPredicate, int pageLimit, int[] retries, bool verbose) where T : Issue
    {
        int pageNumber = 1;
        string? after = null;
        bool hasNextPage = true;
        int loadedCount = 0;
        int? totalCount = null;
        byte retry = 0;

        while (hasNextPage && pageNumber <= pageLimit)
        {
            Console.WriteLine($"Downloading {itemQueryName} page {pageNumber}... (limit: {pageLimit}){(retry > 0 ? $" (retry: {retry} of {retries.Length})" : "")}");

            Page<T> page;

            try
            {
                page = await GetItemsPage<T>(githubToken, org, repo, after, itemQueryName);
            }
            catch (Exception ex) when (
                ex is HttpIOException ||
                ex is HttpRequestException ||
                ex is GraphQLHttpRequestException ||
                ex is TaskCanceledException
            )
            {
                Console.WriteLine($"Exception caught during query.\n  {ex.Message}");

                if (++retry >= retries.Length)
                {
                    Console.WriteLine($"Retry limit of {retries.Length} reached. Aborting.");
                    break;
                }
                else
                {
                    Console.WriteLine($"Waiting {(retries[retry])} seconds before retrying...");
                    await Task.Delay(retries[retry] * 1000);
                    continue;
                }
            }

            if (after == page.EndCursor)
            {
                Console.WriteLine($"Paging did not progress. Cursor: '{after}'. Aborting.");
                break;
            }

            pageNumber++;
            after = page.EndCursor;
            hasNextPage = page.HasNextPage;
            loadedCount += page.Nodes.Length;
            totalCount ??= page.TotalCount;
            retry = 0;

            foreach (T item in page.Nodes)
            {
                // If there are more labels, there might be other applicable
                // labels that were not loaded and the model is incomplete.
                if (item.Labels.HasNextPage)
                {
                    if (verbose) Console.WriteLine($"{itemQueryName} {org}/{repo}#{item.Number} - Excluded from output. Not all labels were loaded.");
                    continue;
                }

                // Only items with exactly one applicable label are used for the model.
                string[] labels = Array.FindAll(item.LabelNames, labelPredicate);
                if (labels.Length != 1)
                {
                    if (verbose) Console.WriteLine($"{itemQueryName} {org}/{repo}#{item.Number} - Excluded from output. {labels.Length} applicable labels found.");
                    continue;
                }

                // Exactly one applicable label was found on the item. Include it in the model.
                if (verbose) Console.WriteLine($"{itemQueryName} {org}/{repo}#{item.Number} - Included in output. Applicable label: '{labels[0]}'.");

                yield return (item, labels[0]);
            }

            Console.WriteLine($"Total {itemQueryName} downloaded: {loadedCount} of {totalCount}. Cursor: '{after}'. {(hasNextPage ? (pageNumber <= pageLimit ? "Continuing to next page..." : "Page limit reached. Finished.") : "No more pages.")}");
        }
    }

    private static async Task<Page<T>> GetItemsPage<T>(string githubToken, string org, string repo, string? after, string itemQueryName) where T : Issue
    {
        using GraphQLHttpClient client = CreateGraphQLClient(githubToken);

        string files = typeof(T) == typeof(PullRequest) ? "files (first: 100) { nodes { path } }" : "";

        GraphQLRequest query = new GraphQLRequest
        {
            Query = $$"""
                query ($owner: String!, $repo: String!, $after: String) {
                    repository (owner: $owner, name: $repo) {
                        result:{{itemQueryName}} (after: $after, first: 100, orderBy: {field: CREATED_AT, direction: DESC}) {
                            nodes {
                                number
                                title
                                body: bodyText
                                labels (first: 25) {
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

        return (await client.SendQueryAsync<RepositoryQuery<Page<T>>>(query)).Data.Repository.Result;
    }

    public static async Task<Issue> GetIssue(string githubToken, string org, string repo, int number) =>
        await GetItem<Issue>(githubToken, org, repo, number, "issue");

    public static async Task<PullRequest> GetPullRequest(string githubToken, string org, string repo, int number) =>
        await GetItem<PullRequest>(githubToken, org, repo, number, "pullRequest");

    private static async Task<T> GetItem<T>(string githubToken, string org, string repo, int number, string itemQueryName) where T : Issue
    {
        using GraphQLHttpClient client = CreateGraphQLClient(githubToken);

        string files = typeof(T) == typeof(PullRequest) ? "files (first: 100) { nodes { path } }" : "";

        GraphQLRequest query = new GraphQLRequest
        {
            Query = $$"""
                query ($owner: String!, $repo: String!, $number: Int!) {
                    repository (owner: $owner, name: $repo) {
                        result:{{itemQueryName}} (number: $number) {
                            number
                            title
                            body: bodyText
                            labels (first: 25) {
                                nodes { name },
                                pageInfo { hasNextPage }
                            }
                            {{files}}
                        }
                    }
                }
                """,
            Variables = new
            {
                Owner = org,
                Repo = repo,
                Number = number
            }
        };

        return (await client.SendQueryAsync<RepositoryQuery<T>>(query)).Data.Repository.Result;
    }

    public static async Task AddLabel(string githubToken, string org, string repo, int number, string label)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            scheme: "bearer",
            parameter: githubToken);
        client.DefaultRequestHeaders.Accept.Add(new("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        client.DefaultRequestHeaders.Add("User-Agent", "GitHub-ML-Labeler");

        var response = await client.PostAsJsonAsync(
            $"https://api.github.com/repos/{org}/{repo}/issues/{number}/labels",
            new string[] { label },
            CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"GitHub Request to add label failed with status code {response.StatusCode} ({response.ReasonPhrase}).");

            foreach (var h in response.Headers)
            {
                Console.WriteLine($"Response Header: {h.Key} = {string.Join(',', (string[])h.Value)}");
            }

            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }
        else
        {
            Console.WriteLine($"Label '{label}' added to {org}/{repo}#{number}");
        }
    }
}
