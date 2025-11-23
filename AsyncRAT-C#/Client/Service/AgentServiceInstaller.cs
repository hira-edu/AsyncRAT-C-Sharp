using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace Client.Service
{
    [RunInstaller(true)]
    public class AgentServiceInstaller : Installer
    {
        public AgentServiceInstaller()
        {
            var processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem
            };

            var serviceInstaller = new ServiceInstaller
            {
                ServiceName = AgentService.ServiceIdentifier,
                DisplayName = AgentService.ServiceDisplayName,
                Description = AgentService.ServiceDescription,
                StartType = ServiceStartMode.Automatic
            };

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
