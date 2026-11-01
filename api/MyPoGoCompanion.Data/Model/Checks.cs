namespace MyPoGoCompanion.Data.Model;

/// <summary>Check constraints shared by several tables.</summary>
internal static class Checks
{
    public const string Ivs = "attack_iv between 0 and 15 and defense_iv between 0 and 15 and stamina_iv between 0 and 15";

    /// <summary>Levels 1–51 in steps of 0.5 (51 with a Best Buddy).</summary>
    public const string Level = "level between 1 and 51 and level * 2 = floor(level * 2)";

    public const string Language = "language in ('en', 'pt-PT')";
}
