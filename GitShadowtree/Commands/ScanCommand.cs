using System.CommandLine;

namespace GitShadowtree.Commands;

// Tracks newly added files that match the shadowtree patterns.
internal sealed class ScanCommand : Command
{
    public ScanCommand() : base("scan", "Tracks new files that match the shadowtree patterns.")
    {
        SetAction(Run);
    }

    private int Run(ParseResult parseResult)
    {
        var root = Shadowtree.Root();
        Shadowtree.Run(Shadowtree.GitDir(root), root, ["add", .. Shadowtree.LoadPatterns(root)]);
        Console.WriteLine("Added new matching files. Run 'git-shadowtree status' to see the result.");
        return 0;
    }
}
