WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.MapGet("/", () => Results.Text("Squad on AKS deployment probe"));
app.MapGet("/fixture", () =>
    Results.Text(File.ReadAllText("/workspace/fixture/README.md")));
app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", () => Results.Ok());

app.Run();
