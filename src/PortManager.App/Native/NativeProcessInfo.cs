using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PortManager.Native;

/// <summary>
/// Reads process details through a single limited-access handle. This is far cheaper than
/// <see cref="System.Diagnostics.Process"/> (no module enumeration, no system-wide process snapshot,
/// no exceptions for protected processes) and avoids waking the WMI provider host.
/// </summary>
internal static class NativeProcessInfo
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaxLongPath = 32_767;

    internal static SafeProcessHandle? OpenForQuery(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, inheritHandle: false, processId);
        if (!handle.IsInvalid)
        {
            return handle;
        }

        handle.Dispose();
        return null;
    }

    internal static string? QueryImagePath(SafeProcessHandle handle)
    {
        var buffer = new char[1024];
        var size = buffer.Length;
        if (QueryFullProcessImageName(handle, 0, buffer, ref size))
        {
            return new string(buffer, 0, size);
        }

        if (Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
        {
            return null;
        }

        buffer = new char[MaxLongPath];
        size = buffer.Length;
        return QueryFullProcessImageName(handle, 0, buffer, ref size)
            ? new string(buffer, 0, size)
            : null;
    }

    internal static DateTimeOffset? QueryStartTimeUtc(SafeProcessHandle handle) =>
        GetProcessTimes(handle, out var creationTime, out _, out _, out _)
            ? DateTimeOffset.FromFileTime(creationTime).ToUniversalTime()
            : null;

    internal static DateTimeOffset? QueryStartTimeUtc(int processId)
    {
        using var handle = OpenForQuery(processId);
        return handle is null ? null : QueryStartTimeUtc(handle);
    }

    internal static string? QueryCommandLine(SafeProcessHandle handle)
    {
        _ = NtQueryInformationProcess(handle, ProcessCommandLineInformation, IntPtr.Zero, 0, out var length);
        if (length <= 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            var status = NtQueryInformationProcess(handle, ProcessCommandLineInformation, buffer, length, out _);
            if (status < 0)
            {
                return null;
            }

            // The buffer starts with a UNICODE_STRING: USHORT Length, USHORT MaximumLength, PWSTR Buffer.
            var byteLength = unchecked((ushort)Marshal.ReadInt16(buffer));
            var text = Marshal.ReadIntPtr(buffer, IntPtr.Size);
            return byteLength == 0 || text == IntPtr.Zero
                ? null
                : Marshal.PtrToStringUni(text, byteLength / sizeof(char));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Matches <see cref="System.Diagnostics.Process.ProcessName"/>, which drops only an ".exe" suffix.</summary>
    internal static string GetProcessName(string imagePath)
    {
        var fileName = Path.GetFileName(imagePath);
        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^4]
            : fileName;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        SafeProcessHandle processHandle,
        uint flags,
        [Out] char[] imageName,
        ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(
        SafeProcessHandle processHandle,
        out long creationTime,
        out long exitTime,
        out long kernelTime,
        out long userTime);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        SafeProcessHandle processHandle,
        int processInformationClass,
        IntPtr processInformation,
        int processInformationLength,
        out int returnLength);
}
