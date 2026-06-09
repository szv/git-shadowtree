namespace GitShadowtree;

/// <summary>Path resolution and git helpers for the shadowtree (work tree overlaid on a bare git-dir).</summary>
internal static class Shadowtree
{
    /// <summary>Work-tree file holding the patterns (gitignore syntax), tracked by the shadowtree.</summary>
    public const string PatternsFile = ".shadowtree";

    /// <summary>Start of the tool-managed block in the main repo's info/exclude.</summary>
    public const string ExcludeBegin = "# BEGIN git shadowtree (managed) - do not edit; mirrors .shadowtree";
    /// <summary>End of the tool-managed block in the main repo's info/exclude.</summary>
    public const string ExcludeEnd = "# END git shadowtree (managed)";

    /// <summary>Marker identifying the tool-managed post-checkout hook (so we never clobber a foreign one).</summary>
    public const string HookMarker = "# BEGIN git shadowtree (managed)";

    /// <summary>Advice printed when a foreign post-checkout hook is found and left untouched.</summary>
    public const string ForeignHookNotice =
        "Note: an existing post-checkout hook was left untouched. Add this line to it so new worktrees "
        + "get the shadowtree:\n  git-shadowtree hook post-checkout \"$1\" \"$2\" \"$3\" >/dev/null 2>&1 || true";

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

    /// <summary>
    /// Configures a freshly-created bare git-dir (as <c>clone</c> does), checks out <c>main</c> into the
    /// work tree, and mirrors the patterns into info/exclude. Returns the checkout exit code (0 on success).
    /// </summary>
    public static int Provision(string gitDir, string root)
    {
        Run(gitDir, root, "config", "status.showUntrackedFiles", "no");
        Run(gitDir, root, "config", "core.autocrlf", "false");

        // A bare clone mirrors refs into refs/heads/* and sets no upstream, so a plain push/pull
        // wouldn't know where main goes. Wire it up so they just work.
        Run(gitDir, root, "config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");
        Run(gitDir, root, "config", "branch.main.remote", "origin");
        Run(gitDir, root, "config", "branch.main.merge", "refs/heads/main");
        // Seed the remote-tracking ref from the cloned main (no extra fetch) so the upstream resolves;
        // otherwise checkout/status warn that the upstream "is gone".
        TryRun(gitDir, root, "update-ref", "refs/remotes/origin/main", "refs/heads/main");

        // Check out main explicitly: a cloned bare remote may keep HEAD on another branch (e.g. master),
        // where a plain checkout -f fails with "branch yet to be born". Surface failures.
        var code = Run(gitDir, root, "checkout", "-f", "main");
        if (code != 0) return code;

        SyncExclude(root, LoadPatterns(root));
        return code;
    }

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
            "# git shadowtree patterns (gitignore syntax). Matching files are tracked in the shadowtree.",
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

    /// <summary>
    /// The hooks directory git uses for this repo: <c>core.hooksPath</c> if set, otherwise the repo's
    /// (common) hooks dir, which is shared across all worktrees - so installing once covers them all.
    /// </summary>
    public static string HooksDir(string root)
    {
        string configured;
        try { configured = Git.Out(root, "config", "--get", "core.hooksPath"); }
        catch (CommandException) { configured = string.Empty; } // unset: git config exits non-zero

        var path = configured.Length > 0 ? configured : Git.Out(root, "rev-parse", "--git-path", "hooks");
        return Path.GetFullPath(path, root);
    }

    /// <summary>
    /// Installs (or refreshes) the post-checkout hook that provisions the shadowtree in new worktrees.
    /// A foreign (non-managed) hook is left untouched unless <paramref name="force"/> is set; returns
    /// <c>false</c> in that case so the caller can advise the user.
    /// </summary>
    public static bool InstallHook(string root, bool force = false)
    {
        var hooksDir = HooksDir(root);
        Directory.CreateDirectory(hooksDir);
        var hookPath = Path.Combine(hooksDir, "post-checkout");

        if (!force && File.Exists(hookPath) && !File.ReadAllText(hookPath).Contains(HookMarker))
            return false;

        // LF endings, no BOM (File.WriteAllText defaults to UTF-8 without BOM) - required by /bin/sh.
        File.WriteAllText(hookPath, PostCheckoutHook());

        // Git for Windows runs hooks via its bundled sh regardless of the exec bit; elsewhere set it.
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(hookPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return true;
    }

    /// <summary>
    /// The managed post-checkout hook. Kept deliberately thin - all logic lives in the tool
    /// (<c>git shadowtree hook post-checkout</c>) so an installed hook never needs rewriting.
    /// </summary>
    private static string PostCheckoutHook() => string.Join('\n',
        "#!/bin/sh",
        HookMarker + " - do not edit",
        "# Provisions the shadowtree in freshly-added worktrees (git worktree add).",
        // Plain `git worktree add` exports none of these, so this is insurance for tool-driven worktree
        // creation (e.g. invoked from within another git hook, or by a wrapper/CI that pins the git env).
        // Without it our checkout would write into the *caller's* index, not the new shadowtree's:
        // --git-dir/--work-tree do NOT override GIT_INDEX_FILE (it has no flag), so an inherited one
        // makes `checkout` overwrite the MAIN repo's index with the shadow tree -> corruption
        // (`fatal: unable to read <oid>`, since those blobs only exist in the shadow object store).
        "# Clear inherited git env so the tool resolves the new worktree, not the caller's repo/index.",
        "unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE GIT_COMMON_DIR GIT_OBJECT_DIRECTORY GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "git-shadowtree hook post-checkout \"$1\" \"$2\" \"$3\" >/dev/null 2>&1",
        "exit 0", // post-checkout's exit code is propagated by `git worktree add`; never fail it.
        "# END git shadowtree (managed)",
        "");
}
