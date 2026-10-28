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
        string root = Path.Combine(Path.GetTempPath(), "squad-review-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new DisposableFixtureWorkspace(fixtureSource, Path.Combine(root, "workspace"),
                NullLogger<DisposableFixtureWorkspace>.Instance);
            workspace.Reset();
            string generation = workspace.Generation;
            workspace.Reset();
            Require(generation != workspace.Generation, "Workspace reset must change the browser generation.");
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
                NullLogger<WorkEvidenceCollector>.Instance, new(TimeSpan.FromMilliseconds(150)));
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
}
