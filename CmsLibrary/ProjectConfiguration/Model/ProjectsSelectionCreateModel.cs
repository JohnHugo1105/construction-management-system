using CmsLibrary.ProjectConfiguration.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.ProjectConfiguration.Model {
    public class ProjectsSelectionCreateModel : IProjectsSelectionCreateCredentials {

        public string Selection {
            get; set;
        }

        public bool MainProjects {
            get; set;
        }

        public bool SubProjects {
            get; set;
        }

        public ProjectsSelectionCreateModel( ) {

        }

        public ProjectsSelectionCreateModel( string selection, bool mainProjects, bool subProjects) {

            Selection = selection;
            MainProjects = mainProjects;
            SubProjects = subProjects;

        }

    }
}
