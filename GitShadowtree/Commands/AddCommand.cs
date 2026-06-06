using System.CommandLine;

namespace GitShadowtree.Commands;

/// <summary>Stages files matching the shadowtree patterns; patterns that match nothing are skipped silently.</summary>
internal sealed class AddCommand : Command
{
    public AddCommand() : base("add", "Stages files matching the shadowtree patterns (new, modified, and deleted).")
    {
        SetAction(Run);
    }

    private int Run(ParseResult parseResult)
    {
        var root = Shadowtree.Root();
        Shadowtree.StagePatterns(Shadowtree.GitDir(root), root, Shadowtree.LoadPatterns(root));
        Console.WriteLine("Staged matching files. Review with 'git shadowtree status', then 'git shadowtree commit'.");
        return 0;
    }
}
