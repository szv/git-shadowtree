using System.CommandLine;

namespace GitShadowtree.Commands;

/// <summary>
/// Syncs the index to the patterns, commits the shadowtree, then mirrors the patterns into
/// info/exclude. Extra tokens (e.g. -m "msg", --amend) are forwarded to <c>git commit</c>.
/// </summary>
internal sealed class CommitCommand : Command
{
    public CommitCommand() : base("commit", "Commits the shadowtree (syncing the index to the patterns first), then syncs the exclude.")
    {
        // Let git commit flags such as -m / --amend pass through to the forwarded git command.
        TreatUnmatchedTokensAsErrors = false;
        SetAction(Run);
    }

    private int Run(ParseResult parseResult)
    {
        var root = Shadowtree.Root();
        var gitDir = Shadowtree.GitDir(root);
        var patterns = Shadowtree.LoadPatterns(root);

        // Stage matches and prune files no longer covered by a pattern (kept in the work tree).
        var dropped = Shadowtree.SyncIndex(gitDir, root, patterns);

        string[] passthrough = [.. parseResult.UnmatchedTokens];
        var code = Shadowtree.Run(gitDir, root, ["commit", .. passthrough]);

        // Keep info/exclude an exact mirror of the (possibly just-changed) patterns.
        Shadowtree.SyncExclude(root, patterns);

        if (dropped.Count > 0)
            Console.WriteLine($"Stopped tracking (pattern removed, kept in work tree): {string.Join(", ", dropped)}");
        return code;
    }
}
