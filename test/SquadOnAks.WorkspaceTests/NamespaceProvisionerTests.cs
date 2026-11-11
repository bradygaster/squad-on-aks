using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using k8s.Models;
using SquadOnAks.NamespaceProvisioner;

internal static class NamespaceProvisionerTests
{
    public static async Task RunAsync()
    {
        var store = new RecordingResourceStore();
        var validator = new RecordingTokenValidator();
        var provisioner = new AppNamespaceProvisioner(store, "soaks");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IAppNamespaceResourceStore>(store);
        builder.Services.AddSingleton<IServiceAccountTokenValidator>(validator);
        builder.Services.AddSingleton<SquadOnAks.NamespaceProvisioner.IAppNamespaceProvisioner>(provisioner);
        await using WebApplication app = builder.Build();
        app.MapAppNamespaceProvisionerEndpoints();
        await app.StartAsync();

        string address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new(address) };
        try
        {
            using HttpResponseMessage anonymous = await client.PostAsync("/apps/first", null);
            Require(anonymous.StatusCode == HttpStatusCode.Unauthorized &&
                validator.Validations == 0 && store.Resources.Count == 0,
                "Namespace requests without bearer credentials are rejected before provisioning.");

            using var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/apps/first");
            invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
            using HttpResponseMessage invalidToken = await client.SendAsync(invalidRequest);
            Require(invalidToken.StatusCode == HttpStatusCode.Unauthorized &&
                validator.Validations == 1 && store.Resources.Count == 0,
                "Only a validated app manager token can provision app namespaces.");

            using HttpResponseMessage provisioned = await PostAsManagerAsync(client, "/apps/first");
            NamespaceProvisionerResponse? payload =
                await provisioned.Content.ReadFromJsonAsync<NamespaceProvisionerResponse>();
            Require(provisioned.IsSuccessStatusCode && payload?.AppNamespace == "soaks-app-first" &&
                store.Resources.Single().Namespace.Metadata.Name == payload.AppNamespace,
                "An authenticated request provisions the deterministic app namespace and its scoped resources.");

            using HttpResponseMessage invalidApp = await PostAsManagerAsync(client, "/apps/not-valid");
            Require(invalidApp.StatusCode == HttpStatusCode.BadRequest && store.Resources.Count == 1,
                "Invalid app IDs cannot create namespaces.");

            string conflictId = Guid.NewGuid().ToString("N");
            store.ConflictingNamespace = AppNamespaceManifest.Create(conflictId, "soaks").Namespace.Metadata.Name;
            using HttpResponseMessage conflict = await PostAsManagerAsync(client, $"/apps/{conflictId}");
            Require(conflict.StatusCode == HttpStatusCode.Conflict && store.Resources.Count == 1,
                "A namespace ownership conflict is reported without replacing foreign resources.");

            using HttpResponseMessage anonymousDelete = await client.DeleteAsync("/apps/first");
            Require(anonymousDelete.StatusCode == HttpStatusCode.Unauthorized && store.Deleted.Count == 0,
                "Namespace deletion without bearer credentials is rejected.");

            using HttpResponseMessage deleted = await SendAsManagerAsync(client, HttpMethod.Delete, "/apps/first");
            NamespaceProvisionerResponse? deletedPayload =
                await deleted.Content.ReadFromJsonAsync<NamespaceProvisionerResponse>();
            Require(deleted.IsSuccessStatusCode && deletedPayload?.AppNamespace == "soaks-app-first" &&
                store.Deleted.Single() == deletedPayload.AppNamespace,
                "An authenticated delete removes only the app's own namespace.");

            using HttpResponseMessage invalidDelete = await SendAsManagerAsync(client, HttpMethod.Delete, "/apps/not-valid");
            using HttpResponseMessage foreignDelete = await SendAsManagerAsync(client, HttpMethod.Delete, $"/apps/{conflictId}");
            Require(invalidDelete.StatusCode == HttpStatusCode.BadRequest &&
                foreignDelete.StatusCode == HttpStatusCode.Conflict && store.Deleted.Count == 1,
                "Invalid app IDs and namespaces Squad does not own are never deleted.");

            Require(store.Swept.Single() is { } sweep && sweep[AppNamespaceManifest.AppLabel] == "first" &&
                sweep[AppNamespaceManifest.PurposeLabel] == AppNamespaceManifest.CheckPurpose &&
                sweep[AppNamespaceManifest.OwnerLabel] == AppNamespaceManifest.Owner,
                "Deleting an app also sweeps only that app's own leftover check namespaces.");

            string appId = Guid.NewGuid().ToString("N");
            using HttpResponseMessage anonymousCheck = await client.PostAsync($"/apps/{appId}/checks/0123456789abcdef", null);
            Require(anonymousCheck.StatusCode == HttpStatusCode.Unauthorized && store.Resources.Count == 1,
                "Check namespaces need the same app manager credentials.");
            using HttpResponseMessage checkCreated = await PostAsManagerAsync(client, $"/apps/{appId}/checks/0123456789abcdef");
            NamespaceProvisionerResponse? checkPayload = await checkCreated.Content.ReadFromJsonAsync<NamespaceProvisionerResponse>();
            AppNamespaceResources check = store.Resources[^1];
            Require(checkCreated.IsSuccessStatusCode && checkPayload?.AppNamespace == "soaks-check-0123456789abcdef" &&
                check.Namespace.Metadata.Name == checkPayload.AppNamespace &&
                check.Namespace.Metadata.Labels[AppNamespaceManifest.PurposeLabel] == AppNamespaceManifest.CheckPurpose &&
                check.Namespace.Metadata.Labels[AppNamespaceManifest.AppLabel] == appId &&
                check.Namespace.Metadata.Labels["pod-security.kubernetes.io/enforce"] == "restricted" &&
                check.DeployerRoleBinding.Subjects.Single().Name == $"app-deployer-{appId}",
                "A check namespace is restricted, labelled as this app's check copy, and deployable only by this app's deployer.");
            var journeyPeer = check.Boundary.Spec.Ingress.Single().FromProperty
                .Single(peer => peer.PodSelector?.MatchLabels?.ContainsKey("soaks.dev/worker-stage") == true);
            Require(journeyPeer.PodSelector.MatchLabels["soaks.dev/worker-stage"] == AppNamespaceManifest.JourneyStage &&
                journeyPeer.PodSelector.MatchLabels[AppNamespaceManifest.AppLabel] == appId &&
                journeyPeer.NamespaceSelector.MatchLabels["kubernetes.io/metadata.name"] == "soaks" &&
                AppNamespaceManifest.Create(appId, "soaks").Boundary.Spec.Ingress.Single().FromProperty
                    .All(peer => peer.PodSelector?.MatchLabels?.ContainsKey("soaks.dev/worker-stage") != true),
                "Only this app's journey runner may reach its check copy, and it can never reach the live app namespace.");

            using HttpResponseMessage invalidCheck = await PostAsManagerAsync(client, $"/apps/{appId}/checks/Not_Valid");
            using HttpResponseMessage longCheck = await PostAsManagerAsync(client, $"/apps/{appId}/checks/0123456789abcdef0");
            Require(invalidCheck.StatusCode == HttpStatusCode.BadRequest && longCheck.StatusCode == HttpStatusCode.BadRequest,
                "Check identifiers must be short DNS labels.");

            using HttpResponseMessage checkDeleted = await SendAsManagerAsync(client, HttpMethod.Delete, $"/apps/{appId}/checks/0123456789abcdef");
            Require(checkDeleted.IsSuccessStatusCode && store.Deleted[^1] == "soaks-check-0123456789abcdef",
                "A finished check removes its own namespace.");
        }
        finally
        {
            await app.StopAsync();
        }

        Console.WriteLine("PASS namespace provisioner authentication, validation, ownership-conflict responses, owned-namespace deletion and check namespaces");
    }

    private static Task<HttpResponseMessage> PostAsManagerAsync(HttpClient client, string path) =>
        SendAsManagerAsync(client, HttpMethod.Post, path);

    private static Task<HttpResponseMessage> SendAsManagerAsync(HttpClient client, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid");
        return client.SendAsync(request);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record NamespaceProvisionerResponse(string AppNamespace);

    private sealed class RecordingTokenValidator : IServiceAccountTokenValidator
    {
        public int Validations { get; private set; }

        public Task<bool> IsAppManagerAsync(string token, CancellationToken cancellationToken)
        {
            Validations++;
            return Task.FromResult(token == "valid");
        }
    }

    private sealed class RecordingResourceStore : IAppNamespaceResourceStore
    {
        public List<AppNamespaceResources> Resources { get; } = [];
        public string? ConflictingNamespace { get; set; }

        public Task EnsureAsync(AppNamespaceResources resources, CancellationToken cancellationToken)
        {
            if (resources.Namespace.Metadata.Name == ConflictingNamespace)
            {
                throw new InvalidOperationException("The namespace is not owned by Squad on AKS.");
            }

            Resources.Add(resources);
            return Task.CompletedTask;
        }

        public List<string> Deleted { get; } = [];

        public Task DeleteAsync(V1Namespace owned, CancellationToken cancellationToken)
        {
            if (owned.Metadata.Name == ConflictingNamespace)
            {
                throw new InvalidOperationException("The namespace is not owned by Squad on AKS.");
            }

            Deleted.Add(owned.Metadata.Name);
            return Task.CompletedTask;
        }

        public List<IDictionary<string, string>> Swept { get; } = [];

        public Task DeleteLabeledAsync(IDictionary<string, string> labels, CancellationToken cancellationToken)
        {
            Swept.Add(labels);
            return Task.CompletedTask;
        }
    }
}
