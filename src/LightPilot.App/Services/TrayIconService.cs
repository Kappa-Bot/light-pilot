using System.ComponentModel;
using System.IO;
using System.Windows;
using LightPilot.App.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LightPilot.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Window _window;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _autoItem;
    private readonly Forms.ToolStripMenuItem _modeItem;
    private bool _disposed;

    public TrayIconService(MainWindowViewModel viewModel, Window window)
    {
        _viewModel = viewModel;
        _window = window;

        var open = new Forms.ToolStripMenuItem("Open Light Pilot");
        open.Click += (_, _) => ShowWindow();

        _autoItem = new Forms.ToolStripMenuItem();
        _autoItem.Click += (_, _) => _viewModel.ToggleAutoCommand.Execute(null);

        var pauseThirty = new Forms.ToolStripMenuItem("Pause 30 min");
        pauseThirty.Click += (_, _) => _viewModel.PauseThirtyMinutesCommand.Execute(null);

        var pauseHour = new Forms.ToolStripMenuItem("Pause 1 hour");
        pauseHour.Click += (_, _) => _viewModel.PauseOneHourCommand.Execute(null);

        var pauseTomorrow = new Forms.ToolStripMenuItem("Pause until tomorrow");
        pauseTomorrow.Click += (_, _) => _viewModel.PauseUntilTomorrowCommand.Execute(null);

        var resume = new Forms.ToolStripMenuItem("Resume now");
        resume.Click += (_, _) => _viewModel.ResumeCommand.Execute(null);

        _modeItem = new Forms.ToolStripMenuItem { Enabled = false };

        var settings = new Forms.ToolStripMenuItem("Settings");
        settings.Click += (_, _) => _viewModel.OpenSettingsCommand.Execute(null);

        var reset = new Forms.ToolStripMenuItem("Reset defaults");
        reset.Click += (_, _) => _viewModel.ResetCommand.Execute(null);

        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => _viewModel.ExitCommand.Execute(null);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "Light Pilot",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _notifyIcon.ContextMenuStrip.Items.AddRange(new Forms.ToolStripItem[]
        {
            open,
            new Forms.ToolStripSeparator(),
            _autoItem,
            pauseThirty,
            pauseHour,
            pauseTomorrow,
            resume,
            new Forms.ToolStripSeparator(),
            _modeItem,
            new Forms.ToolStripSeparator(),
            settings,
            reset,
            exit
        });
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                ShowWindow();
            }
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        RefreshMenuText();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Drawing.Icon CreateTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "LightPilot.ico");
        if (File.Exists(iconPath))
        {
            return new Drawing.Icon(iconPath);
        }

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
        {
            var associatedIcon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            if (associatedIcon is not null)
            {
                return associatedIcon;
            }
        }

        return Drawing.SystemIcons.Application;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.AutoStatus) or nameof(MainWindowViewModel.CurrentModeText))
        {
            RefreshMenuText();
        }
    }

    private void RefreshMenuText()
    {
        _autoItem.Text = _viewModel.AutoEnabled ? "Turn auto off" : "Turn auto on";
        _autoItem.Checked = _viewModel.AutoEnabled;
        _modeItem.Text = $"Current mode: {_viewModel.CurrentModeText}";
        var tooltip = $"Light Pilot - {_viewModel.AutoStatus}";
        _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : "Light Pilot";
    }

    private void ShowWindow()
    {
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == System.Windows.WindowState.Minimized)
        {
            _window.WindowState = System.Windows.WindowState.Normal;
        }

        _window.Activate();
    }
}
