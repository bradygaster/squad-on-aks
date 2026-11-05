var builder = WebApplication.CreateBuilder(args);

var answers = new[]
{
    "It is certain.",
    "Ask again later.",
    "Signs point to yes.",
    "Don't count on it.",
    "The outlook is good.",
    "Very doubtful."
};

var app = builder.Build();
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/api/answer", () => Results.Ok(new { answer = answers[Random.Shared.Next(answers.Length)] }));
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.Run();
