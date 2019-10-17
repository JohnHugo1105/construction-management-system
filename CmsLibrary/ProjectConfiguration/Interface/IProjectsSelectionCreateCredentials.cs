using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.ProjectConfiguration.Interface {
   public interface IProjectsSelectionCreateCredentials {

         string Selection {
            get; set;
        }

        bool MainProjects {
            get; set;
        }

        bool SubProjects {
            get; set;
        }

    }
}
