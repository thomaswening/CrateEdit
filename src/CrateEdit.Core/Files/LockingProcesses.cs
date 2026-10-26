using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CrateEdit.Core.Files;

/// <summary>
/// Finds out which programs have a file open, so a "file is locked" message can name them.
/// Uses the Windows Restart Manager, the same mechanism Explorer uses for "The action can't be
/// completed because the file is open in …". On other systems, files are not locked this way
/// and the result is always empty.
/// </summary>
public static class LockingProcesses
{
    private const int ErrorMoreData = 234;
    private const int SessionKeyLength = 33; // CCH_RM_SESSION_KEY + 1

    /// <summary>Display names of the programs holding <paramref name="path"/>, or empty if unknown.</summary>
    public static IReadOnlyList<string> Find(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            return FindWithRestartManager(Path.GetFullPath(path));
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or Win32Exception)
        {
            // Best effort: naming the program is a convenience, never a reason to fail.
            return [];
        }
    }

    private static List<string> FindWithRestartManager(string path)
    {
        var key = new char[SessionKeyLength];
        Check(RmStartSession(out var session, 0, key));
        try
        {
            Check(RmRegisterResources(session, 1, [path], 0, IntPtr.Zero, 0, null));

            uint count = 0;
            uint rebootReasons = 0;
            var result = RmGetList(session, out var needed, ref count, null, ref rebootReasons);
            if (result != ErrorMoreData)
            {
                Check(result);
                return [];
            }

            var processes = new RmProcessInfo[needed];
            count = needed;
            Check(RmGetList(session, out _, ref count, processes, ref rebootReasons));
            return processes.Take((int)count)
                .Select(p => p.AppName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();
        }
        finally
        {
            // A session that cannot be ended only holds memory until the process exits; nothing to do.
            _ = RmEndSession(session);
        }
    }

    private static void Check(int result)
    {
        if (result != 0)
        {
            throw new Win32Exception(result);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int ProcessId;
        public FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceShortName;

        public int ApplicationType;
        public uint AppStatus;
        public uint TerminalServicesSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, int sessionFlags, [Out] char[] sessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint sessionHandle, uint fileCount, string[] fileNames,
        uint applicationCount, IntPtr applications, uint serviceCount, string[]? serviceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint sessionHandle, out uint processInfoNeeded, ref uint processInfoCount,
        [In, Out] RmProcessInfo[]? affectedApps, ref uint rebootReasons);
}
