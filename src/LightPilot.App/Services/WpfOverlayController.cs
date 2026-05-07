using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.App.Services;

public sealed class WpfOverlayController : IOverlayController, IDisposable
{
    private readonly Dictionary<string, Window> _windows = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask ApplyAsync(MonitorModel monitor, double opacity, int colorTemperatureKelvin, CancellationToken cancellationToken)
    {
        if (System.Windows.Application.Current is null)
        {
            return ValueTask.CompletedTask;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var window = GetOrCreate(monitor.Id);
            var targetOpacity = Math.Clamp(opacity, 0, 0.35);
            window.Background = new SolidColorBrush(ToWarmColor(colorTemperatureKelvin, targetOpacity));

            if (targetOpacity > 0.01 && !window.IsVisible)
            {
                window.Opacity = 0;
                window.Show();
            }

            var fade = new DoubleAnimation(targetOpacity, TimeSpan.FromSeconds(2))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            fade.Completed += (_, _) =>
            {
                if (targetOpacity <= 0.01)
                {
                    window.Hide();
                }
            };

            if (targetOpacity <= 0.01 && !window.IsVisible)
            {
                window.Opacity = 0;
                return;
            }

            window.BeginAnimation(Window.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        });

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var window in _windows.Values)
        {
            window.Close();
        }

        _windows.Clear();
    }

    private Window GetOrCreate(string monitorId)
    {
        if (_windows.TryGetValue(monitorId, out var existing))
        {
            return existing;
        }

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth,
            Height = SystemParameters.VirtualScreenHeight,
            IsHitTestVisible = false,
            Focusable = false
        };

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var style = GetWindowLong(hwnd, GwlExStyle);
            SetWindowLong(hwnd, GwlExStyle, style | WsExTransparent | WsExLayered | WsExToolWindow);
        };

        _windows[monitorId] = window;
        return window;
    }

    private static System.Windows.Media.Color ToWarmColor(int colorTemperatureKelvin, double opacity)
    {
        if (opacity <= 0)
        {
            return Colors.Transparent;
        }

        var warmth = Math.Clamp((6500 - colorTemperatureKelvin) / 3700d, 0, 1);
        return System.Windows.Media.Color.FromArgb(255, 255, (byte)(214 - (24 * warmth)), (byte)(158 - (48 * warmth)));
    }

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
