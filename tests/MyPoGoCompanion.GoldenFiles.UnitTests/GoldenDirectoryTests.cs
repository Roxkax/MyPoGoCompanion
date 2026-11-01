namespace MyPoGoCompanion.GoldenFiles.UnitTests;

public sealed class GoldenDirectoryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("golden-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private GoldenDirectory Create(params (string Path, string Content)[] files)
    {
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(_root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return new GoldenDirectory(_root);
    }

    // "{}\n" is 3 bytes with this SHA-256
    private const string EmptyObjectSha256 = "ca3d163bab055381827226140568f3bef7eaac187cebd76878e0b63e9e442356";

    private static string Manifest(params (string Path, int Bytes, string Sha256)[] files) =>
        "{\"files\":{" + string.Join(",", files.Select(f => $"\"{f.Path}\":{{\"bytes\":{f.Bytes},\"sha256\":\"{f.Sha256}\"}}")) + "}}";

    [Fact]
    public void Paths_AreUnderTheRoot()
    {
        var golden = new GoldenDirectory(_root);

        Assert.Equal(Path.Combine(_root, "inputs", "gm", "GAME_MASTER.json"), golden.Input(Path.Combine("gm", "GAME_MASTER.json")));
        Assert.Equal(Path.Combine(_root, "outputs", "pvp_strings.json"), golden.Output("pvp_strings"));
    }

    [Fact]
    public void ReadOutput_ParsesTheFile()
    {
        var golden = Create(("outputs/pvp_strings.json", """{"GL":{"cap":1500}}"""));

        Assert.Equal(1500, (int)golden.ReadOutput("pvp_strings")["GL"]!["cap"]!);
    }

    [Fact]
    public void ReadOutput_JsonNull_Throws()
    {
        var golden = Create(("outputs/empty.json", "null"));

        Assert.Throws<InvalidDataException>(() => golden.ReadOutput("empty"));
    }

    [Fact]
    public void CheckManifest_FilesAsFrozen_FindsNoProblems()
    {
        var golden = Create(
            ("outputs/a.json", "{}\n"),
            ("manifest.json", Manifest(("outputs/a.json", 3, EmptyObjectSha256))));

        Assert.Empty(golden.CheckManifest());
    }

    [Fact]
    public void CheckManifest_ChangedMissingAndUnlistedFiles_AreListed()
    {
        var golden = Create(
            ("inputs/a.json", "{} \n"),
            ("inputs/extra.json", "{}\n"),
            ("manifest.json", Manifest(("inputs/a.json", 3, EmptyObjectSha256), ("outputs/gone.json", 3, EmptyObjectSha256))));

        Assert.Equal(
            ["inputs/a.json: changed since it was frozen", "outputs/gone.json: missing", "inputs/extra.json: not in manifest.json"],
            golden.CheckManifest());
    }

    [Fact]
    public void CheckManifest_SameSizeDifferentContent_IsChanged()
    {
        var golden = Create(
            ("outputs/a.json", "[]\n"),
            ("manifest.json", Manifest(("outputs/a.json", 3, EmptyObjectSha256))));

        Assert.Equal(["outputs/a.json: changed since it was frozen"], golden.CheckManifest());
    }

    [Fact]
    public void FindRepositoryRoot_FromASubfolder_FindsTheSolution()
    {
        Create(("MyPoGoCompanion.slnx", "<Solution />"), ("a/b/c.txt", ""));

        Assert.Equal(_root, GoldenDirectory.FindRepositoryRoot(Path.Combine(_root, "a", "b")));
    }

    [Fact]
    public void FindRepositoryRoot_NoSolution_Throws() =>
        Assert.Throws<DirectoryNotFoundException>(() => GoldenDirectory.FindRepositoryRoot(Path.GetPathRoot(_root)!));

    [Fact]
    public void KeeperFilters_PointsAtTheFrozenPythonOutputs()
    {
        var golden = GoldenDirectory.KeeperFilters;

        Assert.EndsWith(Path.Combine("tests", "golden", "keeper-filters"), golden.Root);
        Assert.True(File.Exists(golden.Output("pvp_strings")));
    }
}
