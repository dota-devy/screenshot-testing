namespace NoDbSample;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddRouting();

        var app = builder.Build();

        app.MapGet("/", () => Results.Content(
            "<!doctype html><html><head><title>No-DB Sample Home</title></head>" +
            "<body><h1>Home</h1><p>Anonymous home page rendered without a DbContext.</p></body></html>",
            "text/html"));

        app.MapGet("/hello", () => Results.Content(
            "<!doctype html><html><head><title>No-DB Sample Hello</title></head>" +
            "<body><h1>Hello</h1><p>Second route, also anonymous, no DB.</p></body></html>",
            "text/html"));

        app.Run();
    }
}
