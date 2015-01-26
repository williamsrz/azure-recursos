using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;
using System.IO;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.Build.Workflow.Activities;
using System.IO.Compression;
using Microsoft.TeamFoundation.Lab.Client;


namespace Templates
{
    /// <summary>
    /// Activity para obter um Environment do Lab Management
    /// </summary>
    [BuildActivity(HostEnvironmentOption.All)]
    public sealed class GetLabEnvironment : CodeActivity
    {
        #region Argumentos
        [RequiredArgument]
        public InArgument<string> TFSServer { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSCollection { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSProject { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSUser { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSPassword { get; set; }

        [RequiredArgument]
        public InArgument<string> TFSDomain { get; set; }

        [RequiredArgument]
        public InArgument<string> EnvEnvironmentName { get; set; }

        public OutArgument<LabEnvironment> Result { get; set; }

        public OutArgument<string> ResultUri { get; set; }
        #endregion

        protected override void Execute(CodeActivityContext context)
        {
            // Mapeamento dos parametros para variaveis:
            string tfsServer = context.GetValue(this.TFSServer);
            string tfsCollection = context.GetValue(this.TFSCollection);
            string tfsProject = context.GetValue(this.TFSProject);
            string tfsUser = context.GetValue(this.TFSUser);
            string tfsPassword = context.GetValue(this.TFSPassword);
            string tfsDomain = context.GetValue(this.TFSDomain);
            string envEnvironmentName = context.GetValue(this.EnvEnvironmentName);

            try
            {

                #region Client do TFS e Lab Service

                System.Net.NetworkCredential administratorCredentials =
                new System.Net.NetworkCredential(tfsUser, tfsPassword, tfsDomain);

                string tfsName = tfsServer + "/" + tfsCollection;
                Uri uri = new Uri(tfsName);
                TfsTeamProjectCollection tfs = new TfsTeamProjectCollection(uri, administratorCredentials);

                LabService lab = tfs.GetService<LabService>();

                if (lab == null)
                    throw new System.ArgumentOutOfRangeException("Lab Service não encontrado.");

                #endregion

                #region Obtém o ambiente

                var env = lab.GetLabEnvironments(tfsProject).FirstOrDefault(c => c.Name == envEnvironmentName) ;

                if (env != null)
                {
                    context.TrackBuildMessage("Ambiente encontrado sob URI: '" + env.Uri.ToString() + "'.", BuildMessageImportance.High);
                    context.SetValue(this.Result, env);
                    context.SetValue(this.ResultUri, env.Uri.ToString());
                }

                #endregion

            }
            catch (Exception ex)
            {
                context.TrackBuildError("Ambiente não pode ser obtido:  " + ex.ToString() + ".");
            }
        }
    }
}

