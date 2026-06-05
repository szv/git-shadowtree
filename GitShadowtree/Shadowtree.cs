namespace GitShadowtree;

// Shared path resolution and helpers for the shadowtree. The work tree is the current
// repository; the shadowtree git-dir lives under the user profile, decoupled from the
// main remote. Patterns are stored in gitignore syntax in the <see cref="PatternsFile"/>.
internal static class Shadowtree
{
    // Tracked by the shadowtree itself. Holds the patterns in gitignore syntax.
    public const string PatternsFile = ".shadowtree";

    public static readonly string[] DefaultPatterns =
    [
        "AGENTS.md",
        "**/AGENTS.md",
        "CLAUDE.md",
        "**/CLAUDE.md",
        "**/.agent/**/*.md",
    ];

    // Root of the current repository, used as the shadowtree work tree.
    public static string Root() => Git.Out(Directory.GetCurrentDirectory(), "rev-parse", "--show-toplevel");

    // The bare shadowtree git-dir, kept outside the work tree: %USERPROFILE%\.shadowtrees\<name>.git
    public static string GitDir(string root)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".shadowtrees", new DirectoryInfo(root).Name + ".git");
    }

    // Runs a git command against the shadowtree (bare git-dir overlaid on the work tree).
    public static int Run(string gitDir, string root, params string[] args)
        => Git.Run(root, [$"--git-dir={gitDir}", $"--work-tree={root}", .. args]);

    // Reads the tracked patterns (gitignore syntax), falling back to the defaults.
    public static string[] LoadPatterns(string root)
    {
        var path = Path.Combine(root, PatternsFile);
        if (!File.Exists(path)) return DefaultPatterns;

        var patterns = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        return patterns.Length > 0 ? patterns : DefaultPatterns;
    }

    // Writes the patterns in gitignore syntax (one per line) into the work tree.
    public static void WritePatterns(string root, IReadOnlyList<string> patterns)
    {
        string[] lines =
        [
            "# git-shadowtree patterns (gitignore syntax). Matching files are tracked in the shadowtree.",
            .. patterns,
        ];
        File.WriteAllLines(Path.Combine(root, PatternsFile), lines);
    }

    // Excludes the shadowtree files from the main repository via .git/info/exclude so that
    // they do not show up as untracked there.
    public static void EnsureExcluded(string root, IEnumerable<string> patterns)
    {
        var exclude = Path.GetFullPath(Git.Out(root, "rev-parse", "--git-path", "info/exclude"), root);
        Directory.CreateDirectory(Path.GetDirectoryName(exclude)!);

        var existing = File.Exists(exclude)
            ? new HashSet<string>(File.ReadAllLines(exclude))
            : [];

        var toAdd = new List<string>();
        foreach (var pattern in patterns.Append(PatternsFile))
            if (existing.Add(pattern)) toAdd.Add(pattern);

        if (toAdd.Count > 0) File.AppendAllLines(exclude, toAdd);
    }
}
