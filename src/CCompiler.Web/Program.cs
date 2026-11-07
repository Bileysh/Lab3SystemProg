using System.Text.Encodings.Web;
using System.Text.Json;
using CCompiler;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
});

string root = AppContext.BaseDirectory;
string examplesDir = Path.Combine(root, "examples");

app.MapPost("/api/compile", (CompileRequest req) =>
{
    if (req.Source.Length > 200_000) return Results.BadRequest("Занадто великий файл");
    return Results.Ok(Compiler.Compile(req.Source, req.Name ?? "input.c", interpret: true, trace: req.Trace));
});

app.MapGet("/api/examples", () =>
    Directory.Exists(examplesDir)
        ? Directory.GetFiles(examplesDir, "*.c").Select(Path.GetFileName).Order().ToArray()
        : []);

app.MapGet("/api/examples/{name}", (string name) =>
{
    string path = Path.Combine(examplesDir, Path.GetFileName(name));
    return File.Exists(path) ? Results.Text(File.ReadAllText(path)) : Results.NotFound();
});

app.MapGet("/grammar/{file}", (string file) =>
{
    file = Path.GetFileName(file);
    string path = Path.Combine(root, "grammar", file);
    if (!File.Exists(path))
        path = Path.Combine(app.Environment.ContentRootPath, "..", "CCompiler", "Parsing", file);
    if (!File.Exists(path)) return Results.NotFound();
    string type = path.EndsWith(".html") ? "text/html; charset=utf-8" : "text/plain; charset=utf-8";
    return Results.File(path, type);
});

app.Run();

record CompileRequest(string Source, string? Name, bool Trace);
