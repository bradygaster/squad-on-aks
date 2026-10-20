WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.MapGet("/", () => Results.Text("Squad on AKS deployment probe"));
app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", () => Results.Ok());

app.Run();
