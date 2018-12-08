using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AllowedApplicationsMonitor
{
    class EvaluatorProcess
    {
        [DllImport("Rstrtmgr.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
        public static extern UInt32 RmStartSession(out UInt32 pSessionHandle, UInt32 dwSessionFlags,
            string strSessionKey);

        [DllImport("Rstrtmgr.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
        public static extern UInt32 RmRegisterResources(UInt32 dwSessionHandle,
        UInt32 nFiles, string[] rgsFilenames, UInt32 nApplications,
        ref RM_UNIQUE_PROCESS rgApplications, UInt32 nServices, string[] rgsServiceNames);

        [DllImport("Rstrtmgr.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
        public static extern UInt32 RmGetList(UInt32 dwSessionHandle, out UInt32 pnProcInfoNeeded,
        ref UInt32 pnProcInfo, [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref UInt32 lpdwRebootReasons);

        [DllImport("Rstrtmgr.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
        public static extern UInt32 RmEndSession(UInt32 dwSessionHandle);

        public const UInt32 RmRebootReasonNone = 0x0;
        public const int ERROR_MORE_DATA = 234;
    
        public static System.Runtime.InteropServices.ComTypes.FILETIME FileTimeFromDateTime(DateTime date)
        {
            long ftime = date.ToFileTime();
            System.Runtime.InteropServices.ComTypes.FILETIME ft = new System.Runtime.InteropServices.ComTypes.FILETIME();
            ft.dwHighDateTime = (int)(ftime >> 32);
            ft.dwLowDateTime = (int)ftime;
            return ft;
        }
    
        public static RM_APP_TYPE GetProcessType(Process proc)
        {
            uint handle;
            string key = Guid.NewGuid().ToString();

            uint res = RmStartSession(out handle, (uint)0, key);
            if (res != 0)
            {
                throw new ApplicationException("Could not begin restart session. ");
            }

            try
            {
                uint pnProcInfoNeeded = 0, pnProcInfo = 0,
                    lpdwRebootReasons = RmRebootReasonNone;

                RM_UNIQUE_PROCESS uniqueprocess = new RM_UNIQUE_PROCESS();
                uniqueprocess.dwProcessId = proc.Id;
                System.Runtime.InteropServices.ComTypes.FILETIME ft = FileTimeFromDateTime(proc.StartTime);
                uniqueprocess.ProcessStartTime = ft;

                res = RmRegisterResources(handle, 0, null, 1, ref uniqueprocess, 0, null);

                if (res != 0)
                {
                    throw new ApplicationException("Could not register resource.");
                }

                res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null,
                                ref lpdwRebootReasons);
                if (res == ERROR_MORE_DATA)
                {
                    RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo,
                        processInfo, ref lpdwRebootReasons);
                    if (res == 0)
                    {
                        if (pnProcInfo == 0) throw new ApplicationException("Process not found");

                        return processInfo[0].ApplicationType;
                    }
                    else
                    {
                        throw new ApplicationException("Could not list processes");
                    }
                }
                else if (res != 0)
                {
                    throw new ApplicationException("Failed to get size of result.");
                }
            }
            finally
            {
                RmEndSession(handle);
            }
            throw new ApplicationException("Process not found");
        }
    }
    

    [StructLayout(LayoutKind.Sequential)]
    public struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct RM_PROCESS_INFO
    {
        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;

        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    public enum RM_APP_TYPE
    {
        // The application cannot be classified as any other type. 
        RmUnknownApp = 0,
        // A Windows application run as a stand-alone process that 
        // displays a top-level window. 
        RmMainWindow = 1,
        // A Windows application that does not run as a stand-alone 
        // process and does not display a top-level window. 
        RmOtherWindow = 2,
        // The application is a Windows service. 
        RmService = 3,
        // The application is Windows Explorer. 
        RmExplorer = 4,
        // The application is a stand-alone console application. 
        RmConsole = 5,
        // A system restart is required to complete the installation because 
        // a process cannot be shut down. 
        RmCritical = 1000
    }
}
