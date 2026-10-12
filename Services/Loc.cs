using System.Windows;

namespace GmuLauncher.Services;

/// <summary>إدارة اللغة: نستبدل قاموس النصوص رقم 0 فتتحدّث كل الـ DynamicResource فوراً.</summary>
public static class Loc
{
    public static string T(string key) => Application.Current?.TryFindResource(key) as string ?? key;

    public static void Apply(string lang)
    {
        if (lang != "ar" && lang != "en") lang = "ar";
        var d = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Resources/Strings.{lang}.xaml") };
        Application.Current.Resources.MergedDictionaries[0] = d;
    }
}
