using System.CommandLine;
using GitShadowtree;
using GitShadowtree.Commands;

var rootCommand = new RootCommand(
    "git shadowtree - track a set of files (for example agent docs) in a separate repository, decoupled "
    + "from the main remote. Any command other than init/clone/add/commit/pull/hook/install-hook is forwarded to git against the shadowtree.")
{
    TreatUnmatchedTokensAsErrors = false
};

rootCommand.Subcommands.Add(new InitCommand());
rootCommand.Subcommands.Add(new CloneCommand());
rootCommand.Subcommands.Add(new AddCommand());
rootCommand.Subcommands.Add(new CommitCommand());
rootCommand.Subcommands.Add(new PullCommand());
rootCommand.Subcommands.Add(new HookCommand());
rootCommand.Subcommands.Add(new InstallHookCommand());

var configuration = new InvocationConfiguration { EnableDefaultExceptionHandler = false };

// Anything that is not one of the subcommands above is forwarded to git against the
// shadowtree, so `git shadowtree <git args>` behaves like running git on the shadowtree.
rootCommand.SetAction(parseResult =>
{
    var tokens = parseResult.UnmatchedTokens;
    if (tokens.Count == 0)
        return rootCommand.Parse("--help").Invoke(configuration);

    var root = Shadowtree.Root();
    return Shadowtree.Run(Shadowtree.GitDir(root), root, [.. tokens]);
});

try
{
    return rootCommand.Parse(args).Invoke(configuration);
}
catch (CommandException ex)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Unexpected error: " + ex.Message);
    return 2;
}