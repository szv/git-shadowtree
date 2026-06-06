using System.CommandLine;

namespace GitShadowtree.Commands;

/// <summary>Adopts an existing shadowtree (onboarding): clones it and checks the files into the work tree.</summary>
internal sealed class CloneCommand : Command
{
    private readonly Argument<string> _remote = new("remote")
    {
        Description = "The URL of the remote repository to clone the shadowtree from."
    };

    public CloneCommand() : base("clone", "Adopts an existing shadowtree (onboarding).")
    {
        Aliases.Add("setup");
        Arguments.Add(_remote);
        SetAction(Run);
    }

    private int Run(ParseResult parseResult)
    {
        var remote = parseResult.GetRequiredValue(_remote);

        var root = Shadowtree.Root();
        var gitDir = Shadowtree.GitDir(root);

        if (!Directory.Exists(gitDir))
            Git.Run(root, "clone", "--bare", remote, gitDir);

        Shadowtree.Run(gitDir, root, "config", "status.showUntrackedFiles", "no");
        Shadowtree.Run(gitDir, root, "config", "core.autocrlf", "false");

        // `git clone --bare` mirrors refs into refs/heads/* and sets no upstream, so a plain
        // push/pull wouldn't know where main goes. Match init's setup so they just work.
        Shadowtree.Run(gitDir, root, "config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");
        Shadowtree.Run(gitDir, root, "config", "branch.main.remote", "origin");
        Shadowtree.Run(gitDir, root, "config", "branch.main.merge", "refs/heads/main");
        // Seed the remote-tracking ref from the cloned main (no extra fetch) so the upstream
        // resolves; otherwise checkout/status warn that the upstream "is gone".
        Shadowtree.TryRun(gitDir, root, "update-ref", "refs/remotes/origin/main", "refs/heads/main");

        // Check out `main` explicitly: a cloned bare remote may keep HEAD on another branch (e.g.
        // master), where a plain `checkout -f` fails with "branch yet to be born". Surface failures.
        var code = Shadowtree.Run(gitDir, root, "checkout", "-f", "main"); // Pull the files into the work tree.
        if (code != 0) return code;

        Shadowtree.SyncExclude(root, Shadowtree.LoadPatterns(root));
        Console.WriteLine($"Shadowtree set up: {gitDir}");
        return 0;
    }
}
