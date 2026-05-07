using System.Runtime.InteropServices;
using LightPilot.Core;

namespace LightPilot.Infrastructure;

public sealed class MonitorEnumerator : IMonitorEnumerator
{
    private const uint McCapsBrightness = 0x00000002;

    public ValueTask<IReadOnlyList<MonitorModel>> EnumerateAsync(CancellationToken cancellationToken)
    {
        var monitors = new List<MonitorModel>();
        NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, (hMonitor, _, _, _) =>
        {
            var info = new NativeMethods.MonitorInfoEx();
            info.cbSize = Marshal.SizeOf<NativeMethods.MonitorInfoEx>();
            var name = NativeMethods.GetMonitorInfo(hMonitor, ref info) ? info.szDevice : "Display";
            var id = string.IsNullOrWhiteSpace(name) ? $"display:{monitors.Count + 1}" : $"device:{name}";
            var supportsBrightness = SupportsDdcBrightness(hMonitor);
            monitors.Add(new MonitorModel(id, string.IsNullOrWhiteSpace(name) ? id : name, supportsBrightness, SupportsColorTemperature: true, 15, 100, 0, hMonitor.ToInt64()));
            return true;
        }, nint.Zero);

        if (monitors.Count == 0)
        {
            monitors.Add(new MonitorModel("primary", "Primary display", false, true, 15, 100, 0));
        }

        return ValueTask.FromResult<IReadOnlyList<MonitorModel>>(monitors);
    }

    private static bool SupportsDdcBrightness(nint hMonitor)
    {
        try
        {
            if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count == 0)
            {
                return false;
            }

            var physical = new NativeMethods.PhysicalMonitor[count];
            if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
            {
                return false;
            }

            try
            {
                return physical.Any(item =>
                    NativeMethods.GetMonitorCapabilities(item.hPhysicalMonitor, out var capabilities, out _)
                    && (capabilities & McCapsBrightness) == McCapsBrightness);
            }
            finally
            {
                NativeMethods.DestroyPhysicalMonitors(count, physical);
            }
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }
}
