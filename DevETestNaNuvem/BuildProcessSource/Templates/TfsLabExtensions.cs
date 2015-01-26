using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Lab.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Templates
{
    public static class TfsLabExtensions
    {
        /// <summary>
        /// Obtém ambientes de Lab do TFS
        /// </summary>
        /// <param name="lab">Lab Service</param>
        /// <param name="teamProjectName">Nome do Team project</param>
        /// <returns>Coleção de LabEnvironments</returns>
        public static ICollection<LabEnvironment> GetLabEnvironments(this LabService lab, string teamProjectName)
        {
            LabEnvironmentQuerySpec labSpec = new LabEnvironmentQuerySpec();
            labSpec.Project = teamProjectName;

            ICollection<LabEnvironment> environments = lab.QueryLabEnvironments(labSpec);

            return environments;
        }
    }
}
