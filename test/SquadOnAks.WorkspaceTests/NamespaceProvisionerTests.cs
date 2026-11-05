using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SquadOnAks.NamespaceProvisioner;

internal static class NamespaceProvisionerTests
{
    public static async Task RunAsync()
    {
        var store = new RecordingResourceStore();
        var validator = new RecordingTokenValidator();
        var provisioner = new TestNamespaceProvisioner(store);
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
                "Only a validated Daydream manager token can provision app namespaces.");

            using HttpResponseMessage provisioned = await PostAsManagerAsync(client, "/apps/first");
            NamespaceProvisionerResponse? payload =
                await provisioned.Content.ReadFromJsonAsync<NamespaceProvisionerResponse>();
            Require(provisioned.IsSuccessStatusCode && payload?.AppNamespace == "daydream-app-first" &&
                store.Resources.Single().Namespace.Metadata.Name == payload.AppNamespace,
                "An authenticated request provisions the deterministic app namespace and its scoped resources.");

            using HttpResponseMessage invalidApp = await PostAsManagerAsync(client, "/apps/not-valid");
            Require(invalidApp.StatusCode == HttpStatusCode.BadRequest && store.Resources.Count == 1,
                "Invalid app IDs cannot create namespaces.");

            string conflictId = Guid.NewGuid().ToString("N");
            store.ConflictingNamespace = AppNamespaceManifest.Create(conflictId, "daydream").Namespace.Metadata.Name;
            using HttpResponseMessage conflict = await PostAsManagerAsync(client, $"/apps/{conflictId}");
            Require(conflict.StatusCode == HttpStatusCode.Conflict && store.Resources.Count == 1,
                "A namespace ownership conflict is reported without replacing foreign resources.");
        }
        finally
        {
            await app.StopAsync();
        }

        Console.WriteLine("PASS namespace provisioner authentication, validation, and ownership-conflict responses");
    }

    private static Task<HttpResponseMessage> PostAsManagerAsync(HttpClient client, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
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

        public Task<bool> IsDaydreamManagerAsync(string token, CancellationToken cancellationToken)
        {
            Validations++;
            return Task.FromResult(token == "valid");
        }
    }

    private sealed class TestNamespaceProvisioner(IAppNamespaceResourceStore store)
        : SquadOnAks.NamespaceProvisioner.IAppNamespaceProvisioner
    {
        public async Task<string> EnsureAsync(string appId, CancellationToken cancellationToken)
        {
            AppNamespaceResources resources = AppNamespaceManifest.Create(appId, "daydream");
            await store.EnsureAsync(resources, cancellationToken);
            return resources.Namespace.Metadata.Name;
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
                throw new InvalidOperationException("The namespace is not owned by Daydream.");
            }

            Resources.Add(resources);
            return Task.CompletedTask;
        }
    }
}
