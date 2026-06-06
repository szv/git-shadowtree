namespace GitShadowtree;

/// <summary>Path resolution and git helpers for the shadowtree (work tree overlaid on a bare git-dir).</summary>
internal static class Shadowtree
{
    /// <summary>Work-tree file holding the patterns (gitignore syntax), tracked by the shadowtree.</summary>
    public const string PatternsFile = ".shadowtree";

    /// <summary>Start of the tool-managed block in the main repo's info/exclude.</summary>
    public const string ExcludeBegin = "# BEGIN git-shadowtree (managed) - do not edit; mirrors .shadowtree";
    /// <summary>End of the tool-managed block in the main repo's info/exclude.</summary>
    public const string ExcludeEnd = "# END git-shadowtree (managed)";

    public static readonly string[] DefaultPatterns =
    [
        "AGENTS.md",
        "**/AGENTS.md",
        "CLAUDE.md",
        "**/CLAUDE.md",
        "**/.agent/**/*.md",
    ];

    /// <summary>Root of the current repository, used as the shadowtree work tree.</summary>
    public static string Root() => Git.Out(Directory.GetCurrentDirectory(), "rev-parse", "--show-toplevel");

    /// <summary>The bare shadowtree git-dir, kept under the user profile's .shadowtrees folder.</summary>
    public static string GitDir(string root)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".shadowtrees", new DirectoryInfo(root).Name + ".git");
    }

    /// <summary>Runs a git command against the shadowtree (bare git-dir overlaid on the work tree).</summary>
    public static int Run(string gitDir, string root, params string[] args)
        => Git.Run(root, [$"--git-dir={gitDir}", $"--work-tree={root}", .. args]);

    /// <summary>Like <see cref="Run"/> but best-effort: swallows output and returns the exit code without throwing.</summary>
    public static int TryRun(string gitDir, string root, params string[] args)
        => Git.TryRun(root, [$"--git-dir={gitDir}", $"--work-tree={root}", .. args]);

    /// <summary>Captures stdout of a git command against the shadowtree; throws on a non-zero exit.</summary>
    public static string Out(string gitDir, string root, params string[] args)
        => Git.Out(root, [$"--git-dir={gitDir}", $"--work-tree={root}", .. args]);

    /// <summary>Reads the tracked patterns (gitignore syntax), falling back to the defaults.</summary>
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

    /// <summary>Writes the patterns (gitignore syntax, one per line) into the work-tree <see cref="PatternsFile"/>.</summary>
    public static void WritePatterns(string root, IReadOnlyList<string> patterns)
    {
        string[] lines =
        [
            "# git-shadowtree patterns (gitignore syntax). Matching files are tracked in the shadowtree.",
            .. patterns,
        ];
        File.WriteAllLines(Path.Combine(root, PatternsFile), lines);
    }

    /// <summary>
    /// Stages every match plus <see cref="PatternsFile"/>, one pattern at a time so a non-matching
    /// one does not abort the rest. Picks up deletions; additive only - never untracks.
    /// </summary>
    public static void StagePatterns(string gitDir, string root, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns.Append(PatternsFile))
            TryRun(gitDir, root, "add", "--", pattern);
    }

    /// <summary>
    /// Stages matches (<see cref="StagePatterns"/>), then drops tracked files no longer covered by a
    /// pattern via <c>git rm --cached</c> (kept in the work tree). Returns the dropped files.
    /// </summary>
    public static IReadOnlyList<string> SyncIndex(string gitDir, string root, IReadOnlyList<string> patterns)
    {
        StagePatterns(gitDir, root, patterns);

        var covered = TrackedFiles(gitDir, root, patterns);
        string[] orphans = [.. TrackedFiles(gitDir, root, [])
            .Where(file => file != PatternsFile && !covered.Contains(file))];

        if (orphans.Length > 0)
            TryRun(gitDir, root, ["rm", "--cached", "--quiet", "--", .. orphans]);

        return orphans;
    }

    /// <summary>Tracked paths, optionally restricted to those matching the pathspecs. Uses -z for verbatim, NUL-split paths.</summary>
    private static HashSet<string> TrackedFiles(string gitDir, string root, IReadOnlyList<string> pathspecs)
    {
        string[] args = pathspecs.Count > 0 ? ["ls-files", "-z", "--", .. pathspecs] : ["ls-files", "-z"];
        return [.. Out(gitDir, root, args).Split('\0', StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>
    /// Mirrors the patterns 1:1 into the main repo's info/exclude, inside a marker-delimited block
    /// rewritten on every call. Lines outside the block are preserved.
    /// </summary>
    public static void SyncExclude(string root, IReadOnlyList<string> patterns)
    {
        var exclude = Path.GetFullPath(Git.Out(root, "rev-parse", "--git-path", "info/exclude"), root);
        Directory.CreateDirectory(Path.GetDirectoryName(exclude)!);

        var lines = File.Exists(exclude) ? File.ReadAllLines(exclude).ToList() : [];

        // Drop the previously managed block (between the markers, inclusive) if present.
        var begin = lines.IndexOf(ExcludeBegin);
        if (begin >= 0)
        {
            var end = lines.IndexOf(ExcludeEnd, begin);
            var last = end >= 0 ? end : lines.Count - 1; // tolerate a missing/corrupt end marker
            lines.RemoveRange(begin, last - begin + 1);
            while (begin > 0 && lines[begin - 1].Length == 0) lines.RemoveAt(--begin); // trim blank gap
        }

        // Build the fresh block: the patterns verbatim, plus the .shadowtree file itself.
        string[] block = [ExcludeBegin, .. patterns, PatternsFile, ExcludeEnd];

        if (lines.Count > 0) lines.Add(string.Empty);
        lines.AddRange(block);
        File.WriteAllLines(exclude, lines);
    }
}
