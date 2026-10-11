using System.Net.Http.Headers;
using System.Text.Json;

namespace AgentGap;

public sealed record LibraryModule(string Id, string Name, string Category, string Note);

public sealed class ModuleCatalog
{
    public required IReadOnlyList<LibraryModule> Modules { get; init; }

    public LibraryModule? Find(string name)
    {
        var needle = name.Trim();
        var slug = Program.Slug(needle);
        return Modules.FirstOrDefault(item => item.Id == slug || item.Name.Equals(needle, StringComparison.OrdinalIgnoreCase));
    }

    public static ModuleCatalog Load(string path)
    {
        var modules = JsonSerializer.Deserialize<List<LibraryModule>>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? [];
        return new ModuleCatalog { Modules = modules };
    }
}

public static class PlaceholderFile
{
    public static string Read(string path, string key)
    {
        if (!File.Exists(path)) return "";
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
        values.TryGetValue(key, out var value);
        return value ?? "";
    }
}

public sealed record GapWrite(string Name, string Note);

public sealed class GapWriter(string projectDir, string libraryDir)
{
    public async Task<GapWrite> Fill(string name, ModuleCatalog catalog, string githubToken)
    {
        var known = catalog.Find(name);
        if (known is not null)
        {
            return new GapWrite(known.Name, "Already in the module library. Nothing new was written.");
        }

        var foundUrl = await FindOnGitHub(name, githubToken);
        var id = Program.Slug(name);
        var body = Source(name, id, foundUrl);
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(libraryDir);
        await File.WriteAllTextAsync(Path.Combine(projectDir, id + ".ts"), body);
        await File.WriteAllTextAsync(Path.Combine(libraryDir, id + ".ts"), body);
        var note = foundUrl.Length == 0
            ? "Not in the library or GitHub. Wrote the module into the project and the library. Missing keys use PLACEHOLDER."
            : $"Found it in GitHub and saved a copy in both places. Missing keys use PLACEHOLDER. {foundUrl}";
        return new GapWrite(name, note);
    }

    private static string Source(string name, string id, string foundUrl)
    {
        var header = foundUrl.Length == 0
            ? $"// Created because {name} was not in the library or in GitHub.\n"
            : $"// Ingested from {foundUrl}.\n";
        return header + $$"""
export const id = "{{id}}";
export const title = "{{name}}";
export const sourceUrl = "{{foundUrl}}";

const databaseUrl = process.env.DATABASE_URL || "PLACEHOLDER";
const azureKey = process.env.AZURE_KEY || "PLACEHOLDER";

export function run(input: { text?: string } = {}) {
  const text = String(input.text || "").trim();
  return { id, title, ok: true, sourceUrl, databaseUrl, azureKey, text };
}
""";
    }

    private static async Task<string> FindOnGitHub(string name, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "";
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LD-agent-gap");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var userResponse = await client.GetAsync("https://api.github.com/user");
            if (!userResponse.IsSuccessStatusCode) return "";
            var user = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            var login = user.RootElement.GetProperty("login").GetString() ?? "";
            if (login.Length == 0) return "";
            var url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(name + " user:" + login)}&per_page=5";
            var search = await client.GetAsync(url);
            if (!search.IsSuccessStatusCode) return "";
            var body = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
            if (!body.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0) return "";
            return items[0].GetProperty("html_url").GetString() ?? "";
        }
        catch
        {
            return "";
        }
    }
}
