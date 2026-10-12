namespace GmuLauncher.Services;

public static class Paths
{
    public static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".gmu-launcher");
    public static string Minecraft => Path.Combine(Root, ".minecraft");
    public static string Skins => Path.Combine(Root, "skins");
    public static string DbFile => Path.Combine(Root, "gmu-launcher.db");

    public static void EnsureAll()
    {
        foreach (var d in new[] { Root, Minecraft, Skins, Path.Combine(Minecraft, "versions") })
            Directory.CreateDirectory(d);
    }
}
