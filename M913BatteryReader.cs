using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RedragonBatteryTray;

internal sealed record BatteryReading(
    int Percent,
    byte RawLevel,
    byte Status,
    string ProductId,
    byte[] Report);

internal sealed record StableBatteryReading(
    BatteryReading Reading,
    IReadOnlyList<byte> RawSamples);

internal static class M913BatteryReader
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
    private const int ErrorIoPending = 997;
    private const uint WaitObject0 = 0;
    private const int ReportLength = 17;
    private const int QueryTimeoutMilliseconds = 1800;

    internal static BatteryReading Read()
    {
        List<string> paths = EnumerateHidPaths();
        foreach (string productId in new[] { "fa07", "fa08" })
        {
            List<string> modelPaths = paths
                .Where(path => path.Contains("vid_25a7", StringComparison.OrdinalIgnoreCase) &&
                               path.Contains($"pid_{productId}", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (modelPaths.Count == 0)
                continue;

            string? featurePath = modelPaths.FirstOrDefault(path =>
                path.Contains("mi_01&col07", StringComparison.OrdinalIgnoreCase));
            string? inputPath = modelPaths.FirstOrDefault(path =>
                path.Contains("mi_01&col05", StringComparison.OrdinalIgnoreCase));

            if (featurePath is null || inputPath is null)
                throw new InvalidOperationException(AppText.DeviceInterfacesUnavailable);

            return Query(featurePath, inputPath, productId.ToUpperInvariant());
        }

        throw new InvalidOperationException(AppText.ReceiverNotConnected);
    }

    internal static StableBatteryReading ReadStable(int sampleCount = 5, int delayMilliseconds = 120)
    {
        if (sampleCount < 1)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));

        var readings = new List<BatteryReading>(sampleCount);
        Exception? lastError = null;
        for (int i = 0; i < sampleCount; i++)
        {
            try
            {
                readings.Add(Read());
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (i + 1 < sampleCount && delayMilliseconds > 0)
                Thread.Sleep(delayMilliseconds);
        }

        if (readings.Count == 0)
            throw lastError ?? new InvalidOperationException(AppText.BatteryReadFailed);

        List<BatteryReading> ordered = readings.OrderBy(reading => reading.RawLevel).ToList();
        BatteryReading median = ordered[(ordered.Count - 1) / 2];
        return new StableBatteryReading(median, readings.Select(reading => reading.RawLevel).ToArray());
    }

    private static BatteryReading Query(string featurePath, string inputPath, string productId)
    {
        using SafeFileHandle featureHandle = CreateFile(
            featurePath, GenericRead | GenericWrite, ShareReadWrite, IntPtr.Zero,
            OpenExisting, FileFlagOverlapped, IntPtr.Zero);
        if (featureHandle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), AppText.OpenCommandChannelFailed);

        using SafeFileHandle inputHandle = CreateFile(
            inputPath, GenericRead, ShareReadWrite, IntPtr.Zero,
            OpenExisting, FileFlagOverlapped, IntPtr.Zero);
        if (inputHandle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), AppText.OpenReplyChannelFailed);

        byte[] command = new byte[ReportLength];
        command[0] = 0x08;
        command[1] = 0x04;
        command[^1] = ComputeChecksum(command);

        if (!HidD_SetFeature(featureHandle, command, command.Length))
            throw new Win32Exception(Marshal.GetLastWin32Error(), AppText.CommandNotAcknowledged);

        byte[] response = ReadReportWithTimeout(inputHandle, QueryTimeoutMilliseconds);
        if (response.Length != ReportLength || response[0] != 0x09 || response[1] != 0x04)
            throw new InvalidDataException(AppText.UnexpectedReply);
        if (ComputeReportSum(response) != 0x55)
            throw new InvalidDataException(AppText.InvalidReplyChecksum);

        byte raw = response[6];
        if (raw > 10)
            throw new InvalidDataException(AppText.UnknownBatteryValue(raw));

        return new BatteryReading(raw * 10, raw, response[5], productId, response);
    }

    private static byte[] ReadReportWithTimeout(SafeFileHandle handle, int timeoutMilliseconds)
    {
        IntPtr buffer = Marshal.AllocHGlobal(ReportLength);
        IntPtr eventHandle = CreateEvent(IntPtr.Zero, true, false, null);
        if (eventHandle == IntPtr.Zero)
        {
            Marshal.FreeHGlobal(buffer);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var overlapped = new NativeOverlappedData { EventHandle = eventHandle };
            bool completed = ReadFile(handle, buffer, ReportLength, out uint bytesRead, ref overlapped);
            if (!completed)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != ErrorIoPending)
                    throw new Win32Exception(error, AppText.ReadReplyFailed);

                if (WaitForSingleObject(eventHandle, (uint)timeoutMilliseconds) != WaitObject0)
                {
                    CancelIoEx(handle, ref overlapped);
                    GetOverlappedResult(handle, ref overlapped, out _, true);
                    throw new TimeoutException(AppText.ReplyTimedOut);
                }

                if (!GetOverlappedResult(handle, ref overlapped, out bytesRead, false))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), AppText.ReadReplyFailed);
            }

            if (bytesRead != ReportLength)
                throw new InvalidDataException(AppText.InvalidReplyLength(bytesRead));

            byte[] response = new byte[ReportLength];
            Marshal.Copy(buffer, response, 0, response.Length);
            return response;
        }
        finally
        {
            CloseHandle(eventHandle);
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static byte ComputeChecksum(byte[] report)
    {
        int sum = 0;
        for (int i = 0; i < report.Length - 1; i++)
            sum += report[i];
        return unchecked((byte)(0x55 - sum));
    }

    private static byte ComputeReportSum(byte[] report)
    {
        int sum = 0;
        foreach (byte value in report)
            sum += value;
        return unchecked((byte)sum);
    }

    private static List<string> EnumerateHidPaths()
    {
        HidD_GetHidGuid(out Guid hidGuid);
        IntPtr deviceInfoSet = SetupDiGetClassDevs(
            ref hidGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == new IntPtr(-1))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var paths = new List<string>();
            uint index = 0;
            while (true)
            {
                var interfaceData = new DeviceInterfaceData
                {
                    Size = Marshal.SizeOf<DeviceInterfaceData>()
                };
                if (!SetupDiEnumDeviceInterfaces(
                        deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                {
                    if (Marshal.GetLastWin32Error() == 259)
                        break;
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);
                IntPtr detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet, ref interfaceData, detailBuffer, requiredSize,
                            out _, IntPtr.Zero))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    string? path = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
                    if (!string.IsNullOrWhiteSpace(path))
                        paths.Add(path);
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }

                index++;
            }

            return paths;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public int Flags;
        public UIntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeOverlappedData
    {
        public UIntPtr Internal;
        public UIntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr EventHandle;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_SetFeature(
        SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid,
        uint memberIndex, ref DeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet, ref DeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize,
        out uint requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        SafeFileHandle file, IntPtr buffer, uint bytesToRead,
        out uint bytesRead, ref NativeOverlappedData overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOverlappedResult(
        SafeFileHandle file, ref NativeOverlappedData overlapped,
        out uint bytesTransferred, [MarshalAs(UnmanagedType.Bool)] bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(
        SafeFileHandle file, ref NativeOverlappedData overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEvent(
        IntPtr eventAttributes, [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState, string? name);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
