using System.Windows;
using System.Windows.Media.Animation;

namespace GmuLauncher.Views;

public partial class SplashWindow : Window
{
    public SplashWindow() { InitializeComponent(); }

    /// <summary>أنيميشن الدخول (Fade + Scale) ثم انتظار قصير.</summary>
    public async Task PlayAsync()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var d = TimeSpan.FromMilliseconds(700);
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, d));
        Scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(0.85, 1, d) { EasingFunction = ease });
        Scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(0.85, 1, d) { EasingFunction = ease });
        await Task.Delay(2200);
    }
}
