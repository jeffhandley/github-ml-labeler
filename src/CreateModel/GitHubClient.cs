using System.Text.Json;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GitHubQueries;

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

        return client;
    }

    public static async Task<bool> DownloadIssues(string githubToken, string org, string repo, StreamWriter writer, int pageLimit = 1000)
    {
        await DownloadItems<Issue>(githubToken, org, repo, writer, pageLimit, "issues");
        return true;
    }

    public static async Task<bool> DownloadPullRequests(string githubToken, string org, string repo, StreamWriter writer, int pageLimit = 1000)
    {
        await DownloadItems<PullRequest>(githubToken, org, repo, writer, pageLimit, "pullRequests", """
            files (first: 100) {
                nodes { path }
                totalCount
            }
            """);

        return true;
    }

    private static async Task<bool> DownloadItems<T>(string githubToken, string org, string repo, StreamWriter writer, int pageLimit, string itemQueryName, string? itemQuery = null) where T : Issue
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
                page = await GetItemsPage<T>(githubToken, org, repo, after, itemQueryName, itemQuery);
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
                writer.WriteLine(FormatRecord(item, labels[0]));
                Console.WriteLine($"{itemQueryName} {org}/{repo}#{item.Number}: {labels[0]}");
            }

            writer.Flush();
        }
        while (after is not null && pageNumber <= pageLimit && retries <= retryLimit);

        return true;
    }

    private static async Task<Page<T>> GetItemsPage<T>(string githubToken, string org, string repo, string? after, string itemQueryName, string? itemQuery) where T : Issue
    {
        using GraphQLHttpClient client = CreateGraphQLClient(githubToken);

        GraphQLRequest query = new GraphQLRequest
        {
            Query = $$"""
                query Issues ($owner: String!, $repo: String!, $after: String) {
                    repository(owner: $owner, name: $repo) {
                        items:{{itemQueryName}} (after: $after, first: 100, orderBy: {field: CREATED_AT, direction: DESC}) {
                            nodes {
                                number
                                author { login }
                                title
                                bodyText
                                createdAt
                                labels(first: 25) {
                                    nodes { name },
                                    pageInfo { hasNextPage }
                                }
                                {{itemQuery}}
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

    private static string FormatRecord(Issue issue, string label)
    {
        string sanitize(string text) => text
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Replace('"', '`');

        string author = sanitize(issue.AuthorLogin);
        string title = sanitize(issue.Title);
        string body = sanitize(issue.BodyText);
        string createdAt = issue.CreatedAt.ToString("O");

        string common = $"{issue.Number}\t{createdAt}\t{label}\t{author}\t{title}\t{body}";

        return issue switch
        {
            PullRequest pull => $"pullRequests\t{common}\t{sanitize(string.Join(";", pull.FilePaths))}",
            _ => $"issues\t{common}\t",
        };
    }
}
