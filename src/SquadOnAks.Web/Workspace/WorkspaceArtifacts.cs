using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;

namespace SquadOnAks.Web.Workspace;

public sealed record WorkspaceFileEntry(string Path, long Size, string Kind);

public sealed record WorkspaceFileContent(string Path, long Size, string Kind, string? Text, bool Truncated);

public sealed class WorkspaceArtifacts(DisposableFixtureWorkspace workspace)
{
    public const int MaxTextBytes = 512 * 1024;
    public const long MaxArchiveBytes = 2L * 1024 * 1024 * 1024;
    private static readonly string[] previewRoots = ["", "public", "app", "src", "dist", "web", "site"];
    private static readonly HashSet<string> excludedDirectories = new(StringComparer.Ordinal)
    {
        ".git",
        "node_modules"
    };
    private static readonly HashSet<string> imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".ico", ".bmp"
    };
    private static readonly FileExtensionContentTypeProvider contentTypes = new();

    public string RootPath => workspace.RootPath;

    public IReadOnlyList<WorkspaceFileEntry> ListFiles()
    {
        if (!Directory.Exists(workspace.RootPath))
        {
            return [];
        }

        return EnumerateFiles()
            .Select(file => new WorkspaceFileEntry(
                RelativePath(file.FullName),
                file.Length,
                Classify(file.FullName)))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();
    }

    public WorkspaceFileContent? ReadFile(string relativePath)
    {
        string fullPath = Resolve(relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var info = new FileInfo(fullPath);
        string kind = Classify(fullPath);
        if (kind != "text")
        {
            return new(RelativePath(fullPath), info.Length, kind, null, false);
        }

        using FileStream stream = File.OpenRead(fullPath);
        int length = (int)Math.Min(info.Length, MaxTextBytes);
        byte[] buffer = new byte[length];
        stream.ReadExactly(buffer);
        return new(
            RelativePath(fullPath),
            info.Length,
            kind,
            Encoding.UTF8.GetString(buffer),
            info.Length > MaxTextBytes);
    }

    public (string FullPath, string ContentType)? ResolveRaw(string relativePath)
    {
        string fullPath = Resolve(relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return (fullPath, ContentType(fullPath));
    }

    public string? FindPreviewRoot()
    {
        foreach (string root in previewRoots)
        {
            try
            {
                string candidate = Resolve(root.Length == 0 ? "index.html" : $"{root}/index.html");
                if (File.Exists(candidate))
                {
                    return root;
                }
            }
            catch (InvalidOperationException)
            {
                // Rejected roots are not previewable; the policy logs the rejected path.
            }
        }

        return null;
    }

    public (string FullPath, string ContentType)? ResolvePreview(string? requestPath)
    {
        string? root = FindPreviewRoot();
        if (root is null)
        {
            return null;
        }

        string path = (requestPath ?? string.Empty).Trim('/');
        if (path.Length == 0 || path.EndsWith('/'))
        {
            path += "index.html";
        }

        string relative = root.Length == 0 ? path : $"{root}/{path}";
        string fullPath;
        try
        {
            fullPath = Resolve(relative);
            if (Directory.Exists(fullPath))
            {
                fullPath = Resolve($"{relative}/index.html");
            }
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return File.Exists(fullPath) ? (fullPath, ContentType(fullPath)) : null;
    }

    public async Task WriteZipAsync(Stream output, CancellationToken cancellationToken)
    {
        // Entry finalization can write synchronously. Spool with bounded memory/disk,
        // then drain asynchronously rather than enabling synchronous HTTP writes.
        await using var spool = new FileBufferingWriteStream(
            memoryThreshold: 64 * 1024, bufferLimit: MaxArchiveBytes,
            tempFileDirectoryAccessor: Path.GetTempPath);
        await using (var archive = await ZipArchive.CreateAsync(
            spool, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancellationToken))
        {
            foreach (FileInfo file in EnumerateFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                ZipArchiveEntry entry = archive.CreateEntry(
                    RelativePath(file.FullName),
                    CompressionLevel.Optimal);
                entry.LastWriteTime = file.LastWriteTime;
                await using Stream entryStream = await entry.OpenAsync(cancellationToken);
                await using FileStream source = file.OpenRead();
                await source.CopyToAsync(entryStream, cancellationToken);
            }
        }
        await spool.DrainBufferAsync(output, cancellationToken);
    }

    private IEnumerable<FileInfo> EnumerateFiles()
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(workspace.RootPath));
        while (pending.Count > 0)
        {
            DirectoryInfo directory = pending.Pop();
            foreach (FileSystemInfo item in directory.EnumerateFileSystemInfos())
            {
                if (item.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (item is DirectoryInfo child)
                {
                    if (!excludedDirectories.Contains(child.Name))
                    {
                        pending.Push(child);
                    }
                }
                else if (item is FileInfo file &&
                    !WorkspacePathPolicy.IsSquadRuntimePath(RelativePath(file.FullName)))
                {
                    yield return file;
                }
            }
        }
    }

    private string Resolve(string relativePath) =>
        workspace.PathPolicy.ResolveArtifactPath(relativePath);

    private string RelativePath(string fullPath) =>
        Path.GetRelativePath(workspace.RootPath, fullPath).Replace('\\', '/');

    private static string ContentType(string fullPath) =>
        contentTypes.TryGetContentType(fullPath, out string? type) ? type : "application/octet-stream";

    private static string Classify(string fullPath)
    {
        if (imageExtensions.Contains(Path.GetExtension(fullPath)))
        {
            return "image";
        }

        using FileStream stream = File.OpenRead(fullPath);
        Span<byte> sample = stackalloc byte[Math.Min(8000, (int)Math.Min(stream.Length, 8000))];
        int read = stream.Read(sample);
        return sample[..read].Contains((byte)0) ? "binary" : "text";
    }
}
