namespace AgentGap;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var project = args.Length > 0 ? args[0] : Ask("Project name");
        var modules = args.Length > 1 ? args.Skip(1).ToArray() : Ask("Module names, separated by commas").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (string.IsNullOrWhiteSpace(project) || modules.Length == 0)
        {
            Console.WriteLine("A project name and at least one module name are required.");
            return;
        }

        var catalog = ModuleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "modules.json"));
        var token = PlaceholderFile.Read(Path.Combine(AppContext.BaseDirectory, "placeholders.json"), "GITHUB_TOKEN");
        var writer = new GapWriter(Path.Combine(Directory.GetCurrentDirectory(), "project", Slug(project)), Path.Combine(Directory.GetCurrentDirectory(), "library"));
        foreach (var name in modules)
        {
            var result = await writer.Fill(name.Trim(), catalog, token);
            Console.WriteLine($"{result.Name}: {result.Note}");
        }
    }

    private static string Ask(string label)
    {
        Console.Write($"{label}: ");
        return Console.ReadLine() ?? "";
    }

    public static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
