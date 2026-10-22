using SquadOnAks.Web;
using SquadOnAks.Web.Workspace;

if (args.Contains(WorkspaceInitializer.InitializeArgument, StringComparer.Ordinal))
{
    WorkspaceInitializer.Initialize();
    return;
}

WebApplication app = SquadOnAksApplication.Build(args);
app.Run();
