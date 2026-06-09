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

        // Configure, check out main into the work tree, and mirror the patterns into info/exclude.
        var code = Shadowtree.Provision(gitDir, root);
        if (code != 0) return code;

        // Install the post-checkout hook so future `git worktree add`s get their own shadowtree.
        if (!Shadowtree.InstallHook(root))
            Console.WriteLine(Shadowtree.ForeignHookNotice);

        Console.WriteLine($"Shadowtree set up: {gitDir}");
        return 0;
    }
}
