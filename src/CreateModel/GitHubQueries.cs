namespace GitHubQueries
{
    public class RepositoryQuery<T>
    {
        public required RepositoryItems Repository { get; init; }

        public class RepositoryItems
        {
            public required Page<T> Items { get; init; }
        }
    }

    public class Issue
    {
        public required long Number { get; init; }
        public required Author Author { get; init; }
        public required string Title { get; init; }
        public required string BodyText { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required Page<Label> Labels { get; init; }

        public string AuthorLogin => this.Author.Login ?? "ghost";
        public string[] LabelNames => this.Labels.Nodes.Select(label => label.Name).ToArray();
    }

    public class Author
    {
        public string? Login { get; init; }
    }

    public class Label
    {
        public required string Name { get; init; }
    }

    public class PullRequest : Issue
    {
        public FilesContent? Files { get; init; }

        public class FilesContent : Page<FileNode>
        {
        }

        public class FileNode
        {
            public required string Path { get; init; }
        }

        public string[] FilePaths => this.Files?.Nodes.Select(file => file.Path).ToArray() ?? Array.Empty<string>();
    }

    public class Page<TNode>
    {
        public required TNode[] Nodes { get; init; }
        public PageInfo? PageInfo { get; init; }
        public int? TotalCount { get; init; }

        public bool HasNextPage => this.PageInfo?.HasNextPage ?? false;
        public string? EndCursor => this.PageInfo?.EndCursor;
    }

    public class PageInfo
    {
        public required bool HasNextPage { get; init; }
        public string? EndCursor { get; init; }
    }
}
