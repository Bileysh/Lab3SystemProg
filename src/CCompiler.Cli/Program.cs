using System.Text;
using System.Text.Json;
using CCompiler;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("""
        Використання: CCompiler.Cli <файл.c> [опції]
          --tokens        вивести лексеми
          --ast           вивести AST
          --js            вивести згенерований JavaScript
          --trace         траса LALR-розбору (shift / reduce)
          -o <файл.js>    записати згенерований JavaScript у файл
          --json <файл>   записати AST у JSON (для візуалізації)
          --no-node       не запускати згенерований код у Node.js
        """);
    return 1;
}

string file = args[0];
string source = File.ReadAllText(file);
string? Opt(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

var res = Compiler.Compile(source, Path.GetFileName(file), trace: args.Contains("--trace"));
var g = res.Grammar;
Console.WriteLine($"Граматика: {g.Rules} правил, {g.States} станів LALR(1) [{g.Generator}], розбір: {res.ParseMs:F1} мс");

if (args.Contains("--tokens"))
    foreach (var t in res.Tokens)
        Console.WriteLine($"{t.Line,4}:{t.Column,-3} {t.Type,-14} {t.Text}");
if (args.Contains("--trace"))
    res.Trace.ForEach(Console.WriteLine);
foreach (var w in res.Warnings)
    Console.WriteLine($"попередження (рядок {w.Line}): {w.Message}");
foreach (var e in res.Errors)
    Console.WriteLine($"помилка [{e.Stage}] (рядок {e.Line}): {e.Message}");

if (args.Contains("--ast") && res.AstText is not null)
    Console.WriteLine(res.AstText);
if (Opt("--json") is { } jsonPath && res.Ast is not null)
{
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(res.Ast, new JsonSerializerOptions
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }));
    Console.WriteLine($"AST записано у {jsonPath}");
}
if (!res.Ok) return 2;

if (args.Contains("--js")) Console.WriteLine(res.JavaScript);
if (Opt("-o") is { } outPath)
{
    File.WriteAllText(outPath, res.JavaScript, new UTF8Encoding(false));
    Console.WriteLine($"JavaScript записано у {outPath}  (запуск: node {outPath})");
}

var it = res.Interpreter!;
Console.WriteLine("\n===== Вивід інтерпретатора AST =====");
Console.Write(it.Stdout);
if (it.Error is not null) Console.WriteLine($"[помилка виконання, рядок {it.Error.Line}]: {it.Error.Message}");
Console.WriteLine($"[код повернення: {it.ExitCode}, кроків: {it.Steps}, {it.Ms:F1} мс]");

if (!args.Contains("--no-node"))
{
    var node = Compiler.RunNode(res.JavaScript!);
    if (node is null)
        Console.WriteLine("\n(Node.js не знайдено - згенерований код можна запустити у веб-інтерфейсі)");
    else
    {
        Console.WriteLine("\n===== Вивід згенерованої JavaScript-програми (Node.js) =====");
        Console.Write(node.Stdout);
        if (node.Error is not null) Console.WriteLine(node.Error.Message);
        Console.WriteLine($"[код повернення: {node.ExitCode}, {node.Ms:F0} мс]");
        Console.WriteLine(node.Stdout == it.Stdout ? "\nРезультати збігаються ✔" : "\nРезультати РІЗНЯТЬСЯ ✘");
    }
}
return 0;
