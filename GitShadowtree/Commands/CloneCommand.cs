using System.CommandLine;

namespace GitShadowtree.Commands;

// Adopts an existing shadowtree (onboarding): clones it and checks the files into the work tree.
internal sealed class CloneCommand : Command
{
    private readonly Option<string> _remote = new("--remote", "-r")
    {
        Required = true,
        Description = "The URL of the remote repository to clone the shadowtree from."
    };

    public CloneCommand() : base("clone", "Adopts an existing shadowtree (onboarding).")
    {
        Aliases.Add("setup");
        Options.Add(_remote);
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
        Shadowtree.Run(gitDir, root, "checkout", "-f"); // Pull the shadowtree files into the work tree.

        Shadowtree.EnsureExcluded(root, Shadowtree.LoadPatterns(root));
        Console.WriteLine($"Shadowtree set up: {gitDir}");
        return 0;
    }
}
