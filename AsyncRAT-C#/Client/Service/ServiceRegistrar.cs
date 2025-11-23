using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Client.Service
{
    internal static class ServiceRegistrar
    {
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SC_MANAGER_CREATE_SERVICE = 0x0002;
        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_AUTO_START = 0x00000002;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint SERVICE_CONFIG_DESCRIPTION = 0x00000001;
        private const uint SERVICE_CONFIG_FAILURE_ACTIONS = 0x00000002;
        private const int SC_ACTION_RESTART = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SERVICE_DESCRIPTION
        {
            public string lpDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SC_ACTION
        {
            public int Type;
            public uint Delay;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SERVICE_FAILURE_ACTIONS
        {
            public uint dwResetPeriod;
            public string lpRebootMsg;
            public string lpCommand;
            public uint cActions;
            public IntPtr lpsaActions;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string machineName, string databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateService(
            IntPtr hSCManager,
            string lpServiceName,
            string lpDisplayName,
            uint dwDesiredAccess,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, ref SERVICE_DESCRIPTION lpInfo);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool StartService(IntPtr hService, int dwNumServiceArgs, IntPtr lpServiceArgVectors);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, IntPtr lpInfo);

        internal static bool IsServiceInstalled(string serviceName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    // Touch the Status to confirm existence.
                    ServiceControllerStatus _ = sc.Status;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static void EnsureInstalled(string serviceName, string displayName, string description, string binaryPath)
        {
            if (!IsServiceInstalled(serviceName))
            {
                InstallService(serviceName, displayName, description, binaryPath);
            }

            TryStart(serviceName);
        }

        internal static void Uninstall(string serviceName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    try
                    {
                        if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        }
                    }
                    catch { }
                }
            }
            catch { }

            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero) return;

            try
            {
                IntPtr service = OpenService(scm, serviceName, SERVICE_ALL_ACCESS);
                if (service == IntPtr.Zero) return;

                try
                {
                    DeleteService(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        private static void InstallService(string serviceName, string displayName, string description, string binaryPath)
        {
            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CREATE_SERVICE);
            if (scm == IntPtr.Zero) return;

            string quotedPath = QuoteBinaryPath(binaryPath);
            IntPtr service = IntPtr.Zero;

            try
            {
                service = CreateService(
                    scm,
                    serviceName,
                    displayName,
                    SERVICE_ALL_ACCESS,
                    SERVICE_WIN32_OWN_PROCESS,
                    SERVICE_AUTO_START,
                    SERVICE_ERROR_NORMAL,
                    quotedPath,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null);

                if (service == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    const int ERROR_SERVICE_EXISTS = 1073;
                    if (error == ERROR_SERVICE_EXISTS)
                    {
                        service = OpenService(scm, serviceName, SERVICE_ALL_ACCESS);
                        if (service == IntPtr.Zero)
                            return;
                    }
                    else
                    {
                        return;
                    }
                }

                SetDescription(service, description);
                ConfigureAutoRestart(service);
                TryStart(service);
            }
            finally
            {
                if (service != IntPtr.Zero)
                {
                    CloseServiceHandle(service);
                }
                CloseServiceHandle(scm);
            }
        }

        private static void TryStart(string serviceName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                        return;

                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }
            }
            catch { }
        }

        private static void TryStart(IntPtr serviceHandle)
        {
            try
            {
                StartService(serviceHandle, 0, IntPtr.Zero);
            }
            catch { }
        }

        private static void SetDescription(IntPtr serviceHandle, string description)
        {
            try
            {
                SERVICE_DESCRIPTION desc = new SERVICE_DESCRIPTION { lpDescription = description };
                ChangeServiceConfig2(serviceHandle, SERVICE_CONFIG_DESCRIPTION, ref desc);
            }
            catch { }
        }

        private static void ConfigureAutoRestart(IntPtr serviceHandle)
        {
            try
            {
                SC_ACTION action = new SC_ACTION
                {
                    Type = SC_ACTION_RESTART,
                    Delay = 5000
                };

                IntPtr actionsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SC_ACTION)));
                Marshal.StructureToPtr(action, actionsPtr, false);

                SERVICE_FAILURE_ACTIONS failureActions = new SERVICE_FAILURE_ACTIONS
                {
                    dwResetPeriod = 0,
                    lpCommand = null,
                    lpRebootMsg = null,
                    cActions = 1,
                    lpsaActions = actionsPtr
                };

                IntPtr failurePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_FAILURE_ACTIONS)));
                Marshal.StructureToPtr(failureActions, failurePtr, false);

                ChangeServiceConfig2(serviceHandle, SERVICE_CONFIG_FAILURE_ACTIONS, failurePtr);

                Marshal.FreeHGlobal(actionsPtr);
                Marshal.FreeHGlobal(failurePtr);
            }
            catch { }
        }

        private static string QuoteBinaryPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (path.StartsWith("\"") && path.EndsWith("\""))
                return path;

            return "\"" + path + "\"";
        }
    }
}
