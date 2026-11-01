using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace MyPoGoCompanion.GoldenFiles;

/// <summary>A folder of golden files: <c>inputs/</c>, <c>outputs/</c> and <c>manifest.json</c>.</summary>
/// <param name="root">The folder, e.g. <c>tests/golden/keeper-filters</c>.</param>
public sealed class GoldenDirectory(string root)
{
    /// <summary>The marker that identifies the repository root.</summary>
    public const string SolutionFile = "MyPoGoCompanion.slnx";

    /// <summary>The keeper-filters golden files frozen from the Python build (<c>tests/golden/keeper-filters</c>).</summary>
    public static GoldenDirectory KeeperFilters { get; } = new(
        Path.Combine(FindRepositoryRoot(AppContext.BaseDirectory), "tests", "golden", "keeper-filters"));

    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>The source data the outputs were built from, in the layout of the Python pipeline's <c>data/</c>.</summary>
    public string InputsPath => Path.Combine(Root, "inputs");

    public string OutputsPath => Path.Combine(Root, "outputs");

    /// <summary>Path of an input file, e.g. <c>Input("gm/GAME_MASTER.json")</c>.</summary>
    public string Input(string relativePath) => Path.Combine(InputsPath, relativePath);

    /// <summary>Path of an output file by name, e.g. <c>Output("pvp_strings")</c>.</summary>
    public string Output(string name) => Path.Combine(OutputsPath, name + ".json");

    /// <summary>Reads an output file, e.g. <c>ReadOutput("pvp_strings")</c>.</summary>
    public JsonNode ReadOutput(string name) => ReadJson(Output(name));

    /// <summary>Reads <c>manifest.json</c>: source commits, and the size and SHA-256 of every file.</summary>
    public JsonNode ReadManifest() => ReadJson(Path.Combine(Root, "manifest.json"));

    /// <summary>
    /// Files whose size or SHA-256 differ from <c>manifest.json</c>, files the manifest lists that are
    /// missing, and files present but not listed. Empty when the folder is exactly as frozen.
    /// </summary>
    public IReadOnlyList<string> CheckManifest()
    {
        var listed = ReadManifest()["files"]!.AsObject();
        var problems = new List<string>();
        foreach (var (relativePath, info) in listed)
        {
            var path = Path.Combine(Root, relativePath);
            if (!File.Exists(path))
            {
                problems.Add($"{relativePath}: missing");
                continue;
            }

            var bytes = File.ReadAllBytes(path);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (bytes.Length != (long)info!["bytes"]! || sha256 != (string)info["sha256"]!)
            {
                problems.Add($"{relativePath}: changed since it was frozen");
            }
        }

        foreach (var folder in new[] { InputsPath, OutputsPath })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(Root, path).Replace('\\', '/');
                if (!listed.ContainsKey(relativePath))
                {
                    problems.Add($"{relativePath}: not in manifest.json");
                }
            }
        }

        return problems;
    }

    /// <summary>The nearest folder at or above <paramref name="start"/> that holds <see cref="SolutionFile"/>.</summary>
    public static string FindRepositoryRoot(string start)
    {
        for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, SolutionFile)))
            {
                return folder.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No {SolutionFile} found at or above {start}.");
    }

    private static JsonNode ReadJson(string path) =>
        JsonNode.Parse(File.ReadAllText(path)) ?? throw new InvalidDataException($"{path} holds JSON null.");
}
