using SquadOnAks.NamespaceProvisioner;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHealthChecks()
    .AddCheck("ready", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), ["ready"]);
builder.Services.AddSingleton<IAppNamespaceResourceStore, KubernetesAppNamespaceResourceStore>();
builder.Services.AddSingleton<IAppNamespaceProvisioner, AppNamespaceProvisioner>();
builder.Services.AddSingleton<IServiceAccountTokenValidator, KubernetesServiceAccountTokenValidator>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapAppNamespaceProvisionerEndpoints();

app.Run();

public partial class Program;
