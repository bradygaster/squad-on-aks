using System.Security.Cryptography;
using System.Text;
using k8s.Models;

namespace SquadOnAks.Web.Workspace;

public sealed record AppIdentity(string Id);
public sealed record StaticAppFiles(IReadOnlyDictionary<string, byte[]> Files);
public sealed record StaticAppManifest(V1ConfigMap Content, V1Deployment Deployment, V1Service Service,
    V1NetworkPolicy NetworkPolicy)
{
    public const int MaxContentBytes = 512 * 1024;
    public const int MaxFileBytes = 256 * 1024;
    public const int MaxFiles = 128;
    public const string OwnerLabel = "soaks.dev/owner";
    public const string AppLabel = "soaks.dev/app-id";
    public const string Owner = "static-app";

    public static bool ValidId(string id) =>
        id == AppLibrary.FirstAppId || Guid.TryParseExact(id, "N", out _) && id.All(character => !char.IsAsciiLetterUpper(character));

    public static string Name(string id) => ValidId(id)
        ? "soaks-app-" + id
        : throw new InvalidOperationException("That app could not be found.");

    public static Dictionary<string, string> Labels(string id) => new()
    {
        [OwnerLabel] = Owner,
        [AppLabel] = id
    };

    public static bool IsOwned(V1ObjectMeta? metadata, string id) =>
        metadata?.Name == Name(id) &&
        metadata.Labels?.TryGetValue(OwnerLabel, out string? owner) == true && owner == Owner &&
        metadata.Labels.TryGetValue(AppLabel, out string? app) && app == id;

    public static StaticAppManifest Create(string id, string ns, StaticAppFiles content)
    {
        Validate(content);
        string name = Name(id);
        V1ObjectMeta Metadata() => new() { Name = name, NamespaceProperty = ns, Labels = Labels(id) };
        var binary = new Dictionary<string, byte[]>();
        var items = new List<V1KeyToPath>();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string path, byte[] bytes) in content.Files.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            string key = "file" + binary.Count;
            binary.Add(key, bytes);
            items.Add(new() { Key = key, Path = path });
            hash.AppendData(Encoding.UTF8.GetBytes(path + "\0" + bytes.Length + "\0"));
            hash.AppendData(bytes);
        }
        string revision = Convert.ToHexStringLower(hash.GetHashAndReset());
        var deployment = new V1Deployment
        {
            ApiVersion = "apps/v1",
            Kind = "Deployment",
            Metadata = Metadata(),
            Spec = new()
            {
                Replicas = 1,
                Selector = new() { MatchLabels = Labels(id) },
                Template = new()
                {
                    Metadata = new() { Labels = Labels(id), Annotations = new Dictionary<string, string> { ["soaks.dev/revision"] = revision } },
                    Spec = new()
                    {
                        AutomountServiceAccountToken = false,
                        SecurityContext = new()
                        {
                            RunAsNonRoot = true,
                            RunAsUser = 101,
                            RunAsGroup = 101,
                            FsGroup = 101,
                            SeccompProfile = new() { Type = "RuntimeDefault" }
                        },
                        Containers = [new()
                        {
                            Name = "site", Image = "nginxinc/nginx-unprivileged:1.28-alpine",
                            Ports = [new() { ContainerPort = 8080 }],
                            SecurityContext = new()
                            {
                                AllowPrivilegeEscalation = false, ReadOnlyRootFilesystem = true,
                                Capabilities = new() { Drop = ["ALL"] }
                            },
                            Resources = new()
                            {
                                Requests = new Dictionary<string, ResourceQuantity> { ["cpu"] = new("10m"), ["memory"] = new("16Mi") },
                                Limits = new Dictionary<string, ResourceQuantity> { ["cpu"] = new("100m"), ["memory"] = new("64Mi"), ["ephemeral-storage"] = new("32Mi") }
                            },
                            VolumeMounts = [new() { Name = "content", MountPath = "/usr/share/nginx/html", ReadOnlyProperty = true },
                                new() { Name = "tmp", MountPath = "/tmp" }],
                            ReadinessProbe = new() { HttpGet = new() { Path = "/index.html", Port = 8080 }, PeriodSeconds = 2 },
                            LivenessProbe = new() { HttpGet = new() { Path = "/index.html", Port = 8080 }, InitialDelaySeconds = 5 }
                        }],
                        Volumes = [new() { Name = "content", ConfigMap = new() { Name = name, Items = items } },
                            new() { Name = "tmp", EmptyDir = new() { SizeLimit = new("16Mi") } }]
                    }
                }
            }
        };
        return new(
            new() { ApiVersion = "v1", Kind = "ConfigMap", Metadata = Metadata(), BinaryData = binary },
            deployment,
            new()
            {
                ApiVersion = "v1",
                Kind = "Service",
                Metadata = Metadata(),
                Spec = new() { Type = "ClusterIP", Selector = Labels(id), Ports = [new() { Port = 80, TargetPort = 8080 }] }
            },
            new()
            {
                ApiVersion = "networking.k8s.io/v1",
                Kind = "NetworkPolicy",
                Metadata = Metadata(),
                Spec = new()
                {
                    PodSelector = new() { MatchLabels = Labels(id) },
                    PolicyTypes = ["Ingress", "Egress"],
                    Egress = [],
                    Ingress = [new() { FromProperty = [new() { PodSelector = new() { MatchLabels = new Dictionary<string, string> { ["soaks.dev/host"] = "true" } } }],
                        Ports = [new() { Protocol = "TCP", Port = 8080 }] }]
                }
            });
    }

    public static void Validate(StaticAppFiles content)
    {
        if (!content.Files.ContainsKey("index.html"))
            throw new InvalidOperationException("Your app needs a page to open before it can go online.");
        long total = 0;
        if (content.Files.Count > MaxFiles)
            throw new InvalidOperationException($"The app has too many public files (maximum {MaxFiles}).");
        foreach ((string path, byte[] bytes) in content.Files)
        {
            if (!PublicPath(path))
                throw new InvalidOperationException("The app contains an unsupported public file path.");
            total += bytes.Length;
            if (bytes.Length > MaxFileBytes || total > MaxContentBytes)
                throw new InvalidOperationException("This app is too big to put online. Ask your squad to make a smaller version.");
        }
    }

    /// <summary>Any well-formed path of a deployed Aspire app; extensions are not restricted because APIs are routes.</summary>
    public static bool AppPath(string path) =>
        path.Length is > 0 and <= 300 && !path.Contains('\\') && !path.Contains('%') &&
        path.Split('/').All(segment => segment.Length > 0 && !segment.StartsWith('.') &&
            segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'));

    public static bool PublicPath(string path) =>
        path.Length > 0 && !path.Contains('\\') && !path.Contains('%') &&
        path.Split('/').All(segment => segment.Length > 0 && !segment.StartsWith('.') &&
            segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')) &&
        Path.GetExtension(path).ToLowerInvariant() is
            ".html" or ".css" or ".js" or ".json" or ".png" or ".jpg" or ".jpeg" or ".gif" or
            ".webp" or ".svg" or ".ico" or ".woff" or ".woff2" or ".ttf" or ".txt" or ".webmanifest";
}
