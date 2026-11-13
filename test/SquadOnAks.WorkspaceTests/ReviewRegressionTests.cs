using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Work;
using SquadOnAks.Web.Workspace;

internal static class ReviewRegressionTests
{
    public static async Task RunAsync(string fixtureSource)
    {
        await StalledTurnChecksAsync();
        string root = Path.Combine(Path.GetTempPath(), "squad-review-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new DisposableFixtureWorkspace(fixtureSource, Path.Combine(root, "workspace"),
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            string generation = workspace.Generation;
            workspace.Reset();
            Require(generation != workspace.Generation, "Workspace reset must change the browser generation.");
            var restarted = new DisposableFixtureWorkspace(fixtureSource, workspace.RootPath,
                NullLogger<DisposableFixtureWorkspace>.Instance);
            Require(restarted.TryResume() && restarted.Generation == workspace.Generation,
                "A restart must resume the existing workspace and keep its browser generation.");
            var otherTemplate = new DisposableFixtureWorkspace(
                Path.Combine(Path.GetDirectoryName(fixtureSource)!, DisposableFixtureWorkspace.StarterTemplateName),
                workspace.RootPath, NullLogger<DisposableFixtureWorkspace>.Instance);
            Require(!otherTemplate.TryResume(), "A workspace made from another template must not be resumed.");
            Require(!new DisposableFixtureWorkspace(fixtureSource, Path.Combine(root, "empty"),
                NullLogger<DisposableFixtureWorkspace>.Instance).TryResume(), "An empty path has nothing to resume.");
            var savedWaves = new WorkWaveStore(new WorkEvidenceStore());
            savedWaves.UseStorage(workspace.StatePath("soaks-waves.json"));
            WorkWave delivered = savedWaves.Begin("Make the arcade floor", null);
            delivered.AppendResponse("- Six cabinets glow on the floor");
            savedWaves.Complete(delivered, "Completed", "Work completed.");
            savedWaves.Begin("Interrupted round", null);
            var reloaded = new WorkWaveStore(new WorkEvidenceStore());
            reloaded.UseStorage(workspace.StatePath("soaks-waves.json"));
            Require(reloaded.List().Count == 2 && reloaded.Get(1)!.Summary.State == "Completed" &&
                reloaded.Get(1)!.Response.Contains("Six cabinets"), "Delivered waves must survive a restart.");
            Require(reloaded.Get(2)!.Summary.State == "Cancelled", "A wave cut off by a restart must read as stopped, not active.");
            Require(reloaded.Begin("Next", null).Number == 3, "New waves continue the saved numbering.");
            var checkEvidence = new WorkEvidenceStore();
            var checkedWaves = new WorkWaveStore(checkEvidence);
            var checkRedactor = new SquadOnAks.Web.Security.SecretRedactor("check-secret");
            Require(WorkLifecycleAgent.PreviousValidationFailure(checkedWaves, checkRedactor) is null,
                "A first wave has no earlier check failure to pass along.");
            WorkWave failing = checkedWaves.Begin("Build the app", null);
            checkEvidence.RecordValidation("sh tests/run.sh", 1,
                new string('x', 7000) + "\nProgram.cs(149,65): error CS8110 check-secret");
            checkedWaves.Complete(failing, "Completed", "Work completed.");
            string? context = WorkLifecycleAgent.PreviousValidationFailure(checkedWaves, checkRedactor);
            Require(context is not null && context.Contains("error CS8110", StringComparison.Ordinal) &&
                context.Contains("exit code 1", StringComparison.Ordinal) && context.Length < 6600 &&
                !context.Contains("check-secret", StringComparison.Ordinal),
                "A fix wave sees the bounded, redacted end of the last failed check output.");
            WorkWave passing = checkedWaves.Begin("Fix it", null);
            checkEvidence.RecordValidation("sh tests/run.sh", 0, "checks complete");
            checkedWaves.Complete(passing, "Completed", "Work completed.");
            Require(WorkLifecycleAgent.PreviousValidationFailure(checkedWaves, checkRedactor) is null,
                "Passing checks add no failure context.");
            var artifacts = new WorkspaceArtifacts(workspace);
            string outside = Path.Combine(root, "outside.html");
            await File.WriteAllTextAsync(outside, "outside");
            string index = Path.Combine(workspace.RootPath, "index.html");
            File.CreateSymbolicLink(index, outside);
            Require(artifacts.FindPreviewRoot() is null, "A linked root index must not enable preview.");
            File.Delete(index);
            await File.WriteAllTextAsync(index, "inside");
            string docs = Path.Combine(workspace.RootPath, "docs");
            Directory.CreateDirectory(docs);
            File.CreateSymbolicLink(Path.Combine(docs, "index.html"), outside);
            Require(artifacts.ResolvePreview("docs") is null, "Directory fallback must validate the final index.");
            Require(artifacts.ResolvePreview("docs/") is null, "Trailing-slash index must reject links.");
            Require(artifacts.ResolvePreview("")?.FullPath == index, "A safe root index must remain available.");
            File.Delete(Path.Combine(docs, "index.html"));
            await File.WriteAllTextAsync(Path.Combine(docs, "index.html"), "docs");
            Require(artifacts.ResolvePreview("docs") is not null, "Safe directory indexes must remain available.");

            await File.WriteAllBytesAsync(Path.Combine(workspace.RootPath, "asset.bin"),
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(2 * 1024 * 1024));
            await using var sink = new AsyncOnlyStream();
            await artifacts.WriteZipAsync(sink, CancellationToken.None);
            Require(sink.BytesWritten > 2 * 1024 * 1024, "Archive must stream file data to a non-seekable async sink.");
            using (var zip = new ZipArchive(new MemoryStream(sink.ToArray()), ZipArchiveMode.Read))
            {
                Require(zip.GetEntry("asset.bin")?.Length == 2 * 1024 * 1024, "Streaming archive must finalize a valid directory.");
                Require(!zip.Entries.Any(entry => entry.FullName.StartsWith(".git/")), "ZIP must exclude private metadata.");
            }

            const string copilot = "review-copilot-token";
            const string github = "review-distinct-github-token";
            var evidence = new WorkEvidenceStore();
            var collector = new WorkEvidenceCollector(workspace, evidence, new SecretRedactor(copilot, github),
                NullLogger<WorkEvidenceCollector>.Instance, new(TimeSpan.FromMilliseconds(150)),
                new LocalFixtureValidationRunner());
            var waves = new WorkWaveStore(evidence);
            string? before = await collector.GetHeadAsync();
            WorkWave wave = waves.Begin("Agent commits its own changes", before);
            await File.WriteAllTextAsync(Path.Combine(workspace.RootPath, "agent.txt"), "agent changes");
            await GitAsync(workspace.RootPath, "add", "-A");
            await GitAsync(workspace.RootPath, "-c", "user.name=Test", "-c", "user.email=test@example.invalid",
                "commit", "--quiet", "-m", "Agent checkpoint");
            string? agentHead = await collector.GetHeadAsync();
            await collector.CaptureAsync(wave, runValidation: false);
            Require(waves.Get(1)?.Summary.CommitSha == agentHead && agentHead != before,
                "Agent-created commit SHA must survive a clean index.");
            Require(evidence.GetSnapshot().Items.Any(item => item.Path == "agent.txt"), "Agent commit diff must be retained.");
            WorkWave noChange = waves.Begin("No change", agentHead);
            await collector.CaptureAsync(noChange, runValidation: false);
            Require(waves.Get(2)?.Summary.CommitSha is null, "No-change waves must not invent a checkpoint.");

            await File.WriteAllTextAsync(Path.Combine(workspace.RootPath, "tests", "run.sh"),
                $"printf '%s\\n' '{copilot}' '{github}'\n");
            await collector.CaptureAsync(wave);
            string output = evidence.GetSnapshot().Items.Last(item => item.Kind == "validation").Output!;
            Require(!output.Contains(copilot) && !output.Contains(github) && output.Contains("[REDACTED]"),
                "Validation must redact both distinct credentials.");
            waves.Complete(wave, "Completed", "Done");
            Require(waves.Get(1)!.Evidence.All(item =>
                !(item.Output?.Contains(github) ?? false)), "Saved wave evidence must not expose the publishing token.");

            string late = Path.Combine(workspace.RootPath, "late-marker");
            await File.WriteAllTextAsync(Path.Combine(workspace.RootPath, "tests", "run.sh"),
                "sleep 1\nprintf late > late-marker\n");
            var elapsed = Stopwatch.StartNew();
            try
            {
                await collector.CaptureAsync(wave);
                throw new InvalidOperationException("Hanging validation must time out.");
            }
            catch (TimeoutException)
            {
                Require(elapsed.Elapsed < TimeSpan.FromSeconds(3), "Validation timeout must be bounded.");
            }
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(75)))
            {
                try
                {
                    await collector.CaptureAsync(wave, cancellation.Token);
                    throw new InvalidOperationException("Cancelled validation must stop.");
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
            }
            await collector.CaptureAsync(wave, runValidation: false);
            await Task.Delay(1300);
            Require(!File.Exists(late), "Timeout/cancellation must kill descendants; cleanup must not run generated tests.");
            Console.WriteLine("PASS streaming ZIP async finalization, final index containment, agent checkpoints, dual-token evidence redaction, bounded validation/process-tree cancellation, generation reset");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task GitAsync(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardError = true };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        using Process process = Process.Start(info)!;
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Require(process.ExitCode == 0, error);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class AsyncOnlyStream : Stream
    {
        private readonly MemoryStream buffer = new();
        public long BytesWritten => buffer.Length;
        public byte[] ToArray() => buffer.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new InvalidOperationException("Synchronous response flush.");
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous response write.");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) =>
            buffer.WriteAsync(data, cancellationToken);
        public override Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancellationToken) =>
            buffer.WriteAsync(data, offset, count, cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>A team turn that gets no reply and no recorded activity is stopped with a plain message.</summary>
    private static async Task StalledTurnChecksAsync()
    {
        var limit = TimeSpan.FromMilliseconds(600);
        async Task<(WorkSnapshot Snapshot, Exception? Failure, int Updates)> RunAsync(Microsoft.Agents.AI.AIAgent inner, WorkEvidenceStore evidence)
        {
            var lifecycle = new WorkLifecycle();
            WorkLifecycle.WorkLease lease = lifecycle.TryBegin("casting")!;
            var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            context.Items[WorkLifecycle.LeaseItemKey] = lease;
            var agent = new WorkLifecycleAgent(inner, new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = context }, evidence,
                null, new SecretRedactor("unused-secret"), NullLogger<WorkLifecycleAgent>.Instance, stallLimit: limit);
            int updates = 0;
            Exception? failure = null;
            try
            {
                await foreach (var _ in agent.RunStreamingAsync([new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Plan the app")])
                    .WithCancellation(new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token))
                    updates++;
            }
            catch (Exception exception) { failure = exception; }
            return (lifecycle.GetSnapshot(), failure, updates);
        }

        var silent = await RunAsync(new ScriptedAgent(async (_, token) => { await Task.Delay(Timeout.Infinite, token); }), new WorkEvidenceStore());
        Require(silent.Snapshot is { TerminalResult: WorkTerminalResult.Failed, IsBusy: false } &&
            silent.Snapshot.Message == WorkLifecycleAgent.StalledMessage(limit) &&
            silent.Failure?.Message == WorkLifecycleAgent.StalledMessage(limit),
            "A turn with no reply and no team activity is stopped and fails with a plain message instead of hanging.");

        var busyEvidence = new WorkEvidenceStore();
        var working = await RunAsync(new ScriptedAgent(async (_, token) =>
        {
            // Quiet in chat but visibly working (recorded team activity) for longer than the limit.
            for (int step = 0; step < 8; step++)
            {
                busyEvidence.RecordState("Active", "working");
                await Task.Delay(200, token);
            }
        }, final: "Done."), busyEvidence);
        Require(working.Snapshot.TerminalResult == WorkTerminalResult.Completed && working.Failure is null && working.Updates == 1,
            "Recorded team activity counts as progress, so a long but active turn finishes normally.");

        var streaming = await RunAsync(new ScriptedAgent(async (yield, token) =>
        {
            for (int step = 0; step < 8; step++) { await yield("part "); await Task.Delay(200, token); }
        }), new WorkEvidenceStore());
        Require(streaming.Snapshot.TerminalResult == WorkTerminalResult.Completed && streaming.Updates == 8,
            "Streamed replies count as progress.");
        Console.WriteLine("PASS a stalled team turn is stopped after its limit; streamed replies and team activity keep it alive");
    }

    /// <summary>An agent whose streaming turn is a script: it may emit text, wait, or never answer.</summary>
    private sealed class ScriptedAgent(Func<Func<string, Task>, CancellationToken, Task> script, string? final = null) : Microsoft.Agents.AI.AIAgent
    {
        protected override ValueTask<Microsoft.Agents.AI.AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        protected override ValueTask<Microsoft.Agents.AI.AgentSession> DeserializeSessionCoreAsync(
            System.Text.Json.JsonElement serializedState, System.Text.Json.JsonSerializerOptions? jsonSerializerOptions, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        protected override ValueTask<System.Text.Json.JsonElement> SerializeSessionCoreAsync(
            Microsoft.Agents.AI.AgentSession session, System.Text.Json.JsonSerializerOptions? jsonSerializerOptions, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        protected override Task<Microsoft.Agents.AI.AgentResponse> RunCoreAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, Microsoft.Agents.AI.AgentSession? session,
            Microsoft.Agents.AI.AgentRunOptions? options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        protected override async IAsyncEnumerable<Microsoft.Agents.AI.AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, Microsoft.Agents.AI.AgentSession? session,
            Microsoft.Agents.AI.AgentRunOptions? options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();
            Task run = Task.Run(async () =>
            {
                try { await script(text => channel.Writer.WriteAsync(text, cancellationToken).AsTask(), cancellationToken); channel.Writer.TryComplete(); }
                catch (Exception exception) { channel.Writer.TryComplete(exception); }
            }, CancellationToken.None);
            await foreach (string text in channel.Reader.ReadAllAsync(cancellationToken))
                yield return new Microsoft.Agents.AI.AgentResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, text);
            await run;
            if (final is not null) yield return new Microsoft.Agents.AI.AgentResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, final);
        }
    }
}
