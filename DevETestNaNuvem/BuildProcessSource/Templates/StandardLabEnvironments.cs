using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Lab.Client;
using Microsoft.TeamFoundation.Build.Workflow.Activities;

namespace Templates
{
    /// <summary>
    /// Activity para criar um ambiente padrão usando máquinas virtuais no Azure
    /// </summary>
    [BuildActivity(HostEnvironmentOption.All)]
    public sealed class StandardLabEnvironments : CodeActivity
    {
        #region Parametros
        [RequiredArgument]
        public InArgument<Uri> TFSCollection { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSProjectName { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSUser { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSPassword { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSDomain { get; set; }

        [RequiredArgument]
        public InArgument<string> EnvMachineName { get; set; }

        [RequiredArgument]
        public InArgument<string> EnvMachineRoles { get; set; }

        [RequiredArgument]
        public InArgument<string> EnvEnvironmentName { get; set; }

        [RequiredArgument]
        public InArgument<string> EnvTestController { get; set; }

        [RequiredArgument]
        public InArgument<TfsTeamProjectCollection> CurrentTfsTeamProjectCollection { get; set; }
        #endregion
        
        protected override void Execute(CodeActivityContext context)
        {
            #region Mapeamento dos parametros para variaveis:
            Uri tfsCollection = context.GetValue(this.TFSCollection);
            string tfsProjectName = context.GetValue(this.TFSProjectName);
            string tfsUser = context.GetValue(this.TFSUser);
            string tfsPassword = context.GetValue(this.TFSPassword);
            string tfsDomain = context.GetValue(this.TFSDomain);
            string envMachineName = context.GetValue(this.EnvMachineName);
            string envMachineRoles = context.GetValue(this.EnvMachineRoles);
            string envEnvironmentName = context.GetValue(this.EnvEnvironmentName);
            string envTestController = context.GetValue(this.EnvTestController);
            TfsTeamProjectCollection currentTfsTeamProjectCollection = context.GetValue(this.CurrentTfsTeamProjectCollection);
            bool runAsInteractive = true;
            #endregion

            try
            {
                #region Carregamento do serviço de LAB

                System.Net.NetworkCredential credentials =
                    new System.Net.NetworkCredential(tfsUser, tfsPassword, tfsDomain);

                LabService labService = GetLabService(currentTfsTeamProjectCollection, credentials);

                if (labService == null)
                    throw new System.ArgumentOutOfRangeException("Lab Service não encontrado.");

                #endregion 

                #region Configura o ambiente

                //Remove ambiente atual com o mesmo nome
                RemoveLabEnvironment(labService, tfsProjectName, envEnvironmentName);

                //Cria novo ambiente
                CreateNewLabEnvironment(labService, runAsInteractive, envEnvironmentName, envTestController, tfsProjectName, envMachineName, envMachineRoles, credentials);

                //Refresh do ambiente
                UpdateLabEnvironment(labService, envMachineName, tfsProjectName, envEnvironmentName);

                Console.WriteLine("Ambiente Criado com Sucesso!");


                #endregion
            }
            catch (Exception ex)
            {
                context.TrackBuildError("Criação do Ambiente falhou:  " + ex.ToString() + ".");
            }
        }

        #region funções de suporte
        private LabService GetLabService(string tfsServer, string tfsCollection, System.Net.NetworkCredential credentials)
        {
            string tfsName = tfsServer + "/" + tfsCollection;
            Uri uri = new Uri(tfsName);
            TfsTeamProjectCollection tfs = new TfsTeamProjectCollection(uri, credentials);
            return tfs.GetService<LabService>();
        }

        private LabService GetLabService(TfsTeamProjectCollection tfsTeamProjectCollection, System.Net.NetworkCredential credentials)
        {
            TfsTeamProjectCollection tfs = new TfsTeamProjectCollection(tfsTeamProjectCollection.Uri, credentials);
            return tfs.GetService<LabService>();
        }
        #endregion

        /// <summary>
        /// Remove um ambiente de LAB
        /// </summary>
        /// <param name="labService"></param>
        /// <param name="tfsProjectName"></param>
        /// <param name="environmentName"></param>
        private void RemoveLabEnvironment(LabService labService, string tfsProjectName, string environmentName)
        {
            var labEnvironmentQuerySpec = new LabEnvironmentQuerySpec();
            labEnvironmentQuerySpec.Project = tfsProjectName;
            labEnvironmentQuerySpec.Disposition = LabEnvironmentDisposition.Active;

            var labEnvironments = labService.QueryLabEnvironments(labEnvironmentQuerySpec);

            foreach (LabEnvironment env in labEnvironments)
            {
                if (env.Name == environmentName)
                {
                    Console.WriteLine("Excluindo ambiente lab '" + environmentName + "'.");
                    env.Destroy();
                }
            }
        }

        /// <summary>
        /// Cria o novo ambiente de Lab
        /// </summary>
        /// <param name="labService"></param>
        /// <param name="runAsInteractive"></param>
        /// <param name="environmentName"></param>
        /// <param name="testController"></param>
        /// <param name="tfsProjectName"></param>
        /// <param name="envMachineName"></param>
        /// <param name="envMachineRoles"></param>
        /// <param name="credentials"></param>
        private void CreateNewLabEnvironment(LabService labService, bool runAsInteractive, string environmentName, string testController, string tfsProjectName,
            string envMachineName, string envMachineRoles, System.Net.NetworkCredential credentials)
        {
            Console.WriteLine("Criando o novo ambiente lab '" + environmentName + "'.");

            #region Parametrização e Criação do ambiente de Lab

            LabSystemDefinition labSystemDefinition = new LabSystemDefinition(envMachineName, envMachineName, envMachineRoles);
            LabEnvironmentDefinition labEnvironmentDefinition = new LabEnvironmentDefinition(environmentName, environmentName, new List<LabSystemDefinition>() { labSystemDefinition });

            if (runAsInteractive)
            {
                labEnvironmentDefinition.CodedUIRole = envMachineRoles;
                labEnvironmentDefinition.CodedUIUserName = String.Format("{0}\\{1}", credentials.Domain, credentials.UserName);
            }

            labEnvironmentDefinition.TestControllerName = testController;

            LabEnvironment newEnvironment = labService.CreateLabEnvironment(tfsProjectName, labEnvironmentDefinition, null, null);

            AccountInformation admin = new AccountInformation(credentials.Domain, credentials.UserName, credentials.SecurePassword);
            AccountInformation process = null;

            if (runAsInteractive)
            {
                process = new AccountInformation(credentials.Domain, credentials.UserName, credentials.SecurePassword);
            }
            #endregion

            #region Instalação do test agent
            // Primeira Máquina virtual. para efeito de Demo usando apenas uma máquina.
            // deve-se alterar para procesar várias máquinas
            LabSystem themachine = newEnvironment.LabSystems[0];

            //Instala o agente
            themachine.InstallTestAgent(admin, process);
            #endregion

            #region Aguarda o ambiente ficar em estado PRONTO
            while (newEnvironment.StatusInfo.State != LabEnvironmentState.Ready && newEnvironment.StatusInfo.FailureReason == null)
            {
                Console.WriteLine(String.Format("Status da criação: {0}", newEnvironment.StatusInfo.State));

                foreach (var sm in themachine.StatusMessages)
                {
                    Console.WriteLine(sm.Message);
                }

                System.Threading.Thread.Sleep(9000);

                newEnvironment = labService.QueryLabEnvironments(new LabEnvironmentQuerySpec() { Project = tfsProjectName }).First(f => f.Name == environmentName);
                themachine = newEnvironment.LabSystems[0];
            }
            #endregion
        }

        /// <summary>
        /// Atualiza o ambiente de lab para as novas definições
        /// </summary>
        /// <param name="labService"></param>
        /// <param name="envMachineName"></param>
        /// <param name="tfsProjectName"></param>
        /// <param name="environmentName"></param>
        public void UpdateLabEnvironment(LabService labService, string envMachineName, string tfsProjectName, string environmentName)
        {
            var theEnvironment = labService.QueryLabEnvironments(new LabEnvironmentQuerySpec() { Project = tfsProjectName }).First(f => f.Name == environmentName);

            if (theEnvironment != null)
            {
                var theMachine = theEnvironment.LabSystems.First(f => f.Name == envMachineName);

                if (theMachine != null)
                {
                    string testAgentRunningAs = theMachine.Configuration.ConfiguredUserName;
                    string environmentThinksTestAgentRunningAsd = theEnvironment.CodedUIUserName;

                    if (String.Compare(testAgentRunningAs, environmentThinksTestAgentRunningAsd, true) != 0)
                    {
                        labService.UpdateLabEnvironment(theEnvironment.Uri, new LabEnvironmentUpdatePack() { CodedUIUserName = testAgentRunningAs });
                    }
                }
            }
        }
    }
}
