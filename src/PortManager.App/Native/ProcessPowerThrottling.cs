using System.Runtime.InteropServices;

namespace PortManager.Native;

/// <summary>
/// Toggles Windows EcoQoS for this process. While enabled, Windows schedules the process on
/// efficiency cores at reduced clock speeds, which suits background polling from the tray.
/// </summary>
internal static class ProcessPowerThrottling
{
    private const int ProcessPowerThrottlingInformationClass = 4;
    private const uint PowerThrottlingCurrentVersion = 1;
    private const uint PowerThrottlingExecutionSpeed = 0x1;

    internal static void SetEfficiencyMode(bool enabled)
    {
        var state = new PowerThrottlingState
        {
            Version = PowerThrottlingCurrentVersion,
            ControlMask = PowerThrottlingExecutionSpeed,
            StateMask = enabled ? PowerThrottlingExecutionSpeed : 0
        };

        // Best effort: older Windows builds reject the request, which only means no throttling.
        _ = SetProcessInformation(
            GetCurrentProcess(),
            ProcessPowerThrottlingInformationClass,
            ref state,
            Marshal.SizeOf<PowerThrottlingState>());
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(
        IntPtr processHandle,
        int processInformationClass,
        ref PowerThrottlingState processInformation,
        int processInformationSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }
}
