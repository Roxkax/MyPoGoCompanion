using System.Text.Json;
using MyPoGoCompanion.Engine.Types;

namespace MyPoGoCompanion.Engine.UnitTests;

public sealed class TypeChartTests
{
    [Fact]
    public void BrowserChartMatchesEveryEngineMatchup()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "matchups.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var rows = document.RootElement.EnumerateObject().ToArray();
        Assert.Equal(TypeChart.Types, rows.Select(row => row.Name));

        foreach (var row in rows)
        {
            var powers = row.Value.EnumerateArray().Select(power => power.GetInt32()).ToArray();
            Assert.Equal(TypeChart.Types.Count, powers.Length);
            for (var i = 0; i < powers.Length; i++)
            {
                var defender = TypeChart.Types[i];
                Assert.Equal(TypeChart.Against(row.Name, defender), Math.Pow(TypeChart.SuperEffective, powers[i]), 10);
            }
        }
    }
}
