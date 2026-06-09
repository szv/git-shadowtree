using System.CommandLine;

namespace GitShadowtree.Commands;

/// <summary>Creates a new shadowtree from the files currently in the work tree and pushes it.</summary>
internal sealed class InitCommand : Command
{
    private readonly Option<string> _remote = new("--remote", "-r")
    {
        Required = true,
        Description = "The URL of the remote repository the shadowtree is pushed to."
    };

    private readonly Option<string[]> _patterns = new("--pattern", "-p")
    {
        Description = "A gitignore-style pattern of files to track in the shadowtree. May be repeated.",
        AllowMultipleArgumentsPerToken = true
    };

    public InitCommand() : base("init", "Creates a new shadowtree from the current files and pushes it.")
    {
        Options.Add(_remote);
        Options.Add(_patterns);
        SetAction(Run);
    }

    private int Run(ParseResult parseResult)
    {
        var remote = parseResult.GetRequiredValue(_remote);
        var patterns = parseResult.GetValue(_patterns) is { Length: > 0 } value ? value : Shadowtree.DefaultPatterns;

        var root = Shadowtree.Root();
        var gitDir = Shadowtree.GitDir(root);

        if (Directory.Exists(gitDir))
            throw new CommandException($"Shadowtree already exists: {gitDir}");

        Git.Run(root, "init", "--bare", gitDir);
        Shadowtree.Run(gitDir, root, "config", "status.showUntrackedFiles", "no");
        Shadowtree.Run(gitDir, root, "config", "core.autocrlf", "false");

        // Self-describing pattern list in the work tree (tracked by the shadowtree).
        Shadowtree.WritePatterns(root, patterns);

        Shadowtree.StagePatterns(gitDir, root, patterns);
        Shadowtree.Run(gitDir, root, "commit", "-m", "Add shadowtree files");
        Shadowtree.Run(gitDir, root, "branch", "-M", "main");
        Shadowtree.Run(gitDir, root, "remote", "add", "origin", remote);
        var code = Shadowtree.Run(gitDir, root, "push", "-u", "origin", "main");

        // Best-effort: point a local/file remote's HEAD at main so a plain `git clone` of it checks
        // out cleanly. No-op for URL remotes, where the host manages the default branch.
        if (code == 0)
            Git.TryRun(root, $"--git-dir={remote}", "symbolic-ref", "HEAD", "refs/heads/main");

        Shadowtree.SyncExclude(root, patterns);

        // Install the post-checkout hook so future `git worktree add`s get their own shadowtree.
        if (!Shadowtree.InstallHook(root))
            Console.WriteLine(Shadowtree.ForeignHookNotice);

        Console.WriteLine($"Shadowtree created: {gitDir}");
        return code;
    }
}
