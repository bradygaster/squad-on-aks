using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

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
app.MapGet("/api/answer", () => Results.Ok(new { answer = answers[Random.Shared.Next(answers.Length)] }));
app.MapDefaultEndpoints();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.Run();
