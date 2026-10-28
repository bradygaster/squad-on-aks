using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Casting;
using SquadOnAks.Web.Workspace;

internal static class OriginalIntentTests
{
    private const string Guard =
        "\n\n(Squad on AKS casting phase: you may propose or refine the cast, and create the team files only after " +
        "a clear user confirmation. Do not write, scaffold, test, or run any project code or docs in this turn, " +
        "and do not start a first wave. Project work arrives later as separate wave requests.)";

    public static void Run(string source)
    {
        string root = Path.Combine(Path.GetTempPath(), $"intent-tests-{Guid.NewGuid():N}");
        var workspace = new DisposableFixtureWorkspace(source, root, NullLogger<DisposableFixtureWorkspace>.Instance);
        try
        {
            workspace.Reset();
            var store = NewStore(workspace);
            Expect(404, () => store.Get());
            const string original = "  Build Snake.\nKeep exact spacing.  ";
            store.Capture(original);
            store.Capture("A later refinement must not replace the original.");
            Require(NewStore(workspace).Get() == new OriginalIntent(original, "submitted"),
                "Captured original must survive service recreation with exact whitespace.");
            var artifacts = new WorkspaceArtifacts(workspace);
            Require(!artifacts.ListFiles().Any(file => file.Path.Contains("original-intent", StringComparison.Ordinal)),
                "Private original intent record must not appear in artifacts.");
            foreach (string path in new[] { ".git/squad-original-intent.json", ".squad/session-state/initial/events.jsonl" })
            {
                ExpectPrivate(() => artifacts.ReadFile(path));
                ExpectPrivate(() => artifacts.ResolveRaw(path));
            }

            workspace.Reset();
            WriteEvent(workspace, "initial", original + Guard);
            WriteEvent(workspace, "later-work", "Add sound.");
            OriginalIntent recovered = NewStore(workspace).Get();
            Require(recovered == new OriginalIntent(original, "native-session"),
                "Recovery must preserve the exact original and remove only the injected suffix.");
            File.Delete(workspace.PathPolicy.ResolveWorkspacePath(".squad/session-state/initial/events.jsonl"));
            Require(NewStore(workspace).Get() == recovered, "Recovered intent must be durable without runtime logs.");

            workspace.Reset();
            WriteEvent(workspace, "work-only", "Not the casting prompt.");
            Expect(404, () => NewStore(workspace).Get());
            Expect(404, () => NewStore(workspace).Capture("Do not adopt a later refinement."));
            workspace.Reset();
            WriteEvent(workspace, "first", "First cast." + Guard);
            WriteEvent(workspace, "second", "Another cast." + Guard);
            Expect(409, () => NewStore(workspace).Get());

            workspace.Reset();
            WriteEvent(workspace, "corrupt", "placeholder");
            File.WriteAllText(workspace.PathPolicy.ResolveWorkspacePath(".squad/session-state/corrupt/events.jsonl"), "{bad json");
            Expect(422, () => NewStore(workspace).Get());
            workspace.Reset();
            File.WriteAllText(workspace.PathPolicy.ResolveWorkspacePath(".git/squad-original-intent.json"), "{\"Text\":null,\"Source\":\"submitted\"}");
            Expect(422, () => NewStore(workspace).Get());
            workspace.Reset();
            File.WriteAllText(workspace.PathPolicy.ResolveWorkspacePath(".git/squad-original-intent.json"), "invalid json");
            Expect(422, () => NewStore(workspace).Capture("Must not overwrite corrupt data."));

            workspace.Reset();
            string external = Path.Combine(root, "external.jsonl");
            File.WriteAllText(external, JsonSerializer.Serialize(new
            {
                type = "user.message",
                data = new { content = "Outside workspace." + Guard }
            }));
            string session = workspace.PathPolicy.ResolveWorkspacePath(".squad/session-state/link");
            Directory.CreateDirectory(session);
            File.CreateSymbolicLink(Path.Combine(session, "events.jsonl"), external);
            Expect(422, () => NewStore(workspace).Get());
            File.Delete(Path.Combine(session, "events.jsonl"));

            workspace.Reset();
            string git = workspace.PathPolicy.ResolveWorkspacePath(".git");
            string savedGit = Path.Combine(root, "saved-git");
            Directory.Move(git, savedGit);
            File.WriteAllText(git, "gitdir: ../outside");
            Expect(422, () => NewStore(workspace).Get());
            File.Delete(git);
            Directory.Move(savedGit, git);
            Console.WriteLine("PASS original intent exact capture, native recovery, private persistence, missing/corrupt/ambiguous data and path boundaries");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static OriginalIntentStore NewStore(DisposableFixtureWorkspace workspace) =>
        new(workspace, NullLogger<OriginalIntentStore>.Instance);

    private static void WriteEvent(DisposableFixtureWorkspace workspace, string session, string text)
    {
        string path = workspace.PathPolicy.ResolveWorkspacePath($".squad/session-state/{session}/events.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { type = "user.message", data = new { content = text } }) + "\n");
    }

    private static void Expect(int status, Action action)
    {
        try
        {
            action();
        }
        catch (OriginalIntentException exception) when (exception.StatusCode == status)
        {
            return;
        }
        throw new InvalidOperationException($"Expected original-intent failure HTTP {status}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

    }

    private static void ExpectPrivate(Action read)
    {
        try
        {
            read();
        }
        catch (InvalidOperationException exception) when (exception.Message == "Private workspace metadata is not an artifact.")
        {
            return;
        }
        throw new InvalidOperationException("Private metadata must not be readable through artifact APIs.");
    }
}
