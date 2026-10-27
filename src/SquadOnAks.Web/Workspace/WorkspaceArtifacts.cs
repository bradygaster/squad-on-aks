using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;

namespace SquadOnAks.Web.Workspace;

public sealed record WorkspaceFileEntry(string Path, long Size, string Kind);

public sealed record WorkspaceFileContent(string Path, long Size, string Kind, string? Text, bool Truncated);

public sealed class WorkspaceArtifacts(DisposableFixtureWorkspace workspace)
{
    public const int MaxTextBytes = 512 * 1024;
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
            string candidate = root.Length == 0
                ? Path.Combine(workspace.RootPath, "index.html")
                : Path.Combine(workspace.RootPath, root, "index.html");
            if (File.Exists(candidate))
            {
                return root;
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
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (Directory.Exists(fullPath))
        {
            fullPath = Path.Combine(fullPath, "index.html");
        }

        return File.Exists(fullPath) ? (fullPath, ContentType(fullPath)) : null;
    }

    public async Task WriteZipAsync(Stream output, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
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
        workspace.PathPolicy.ResolveWorkspacePath(relativePath);

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
