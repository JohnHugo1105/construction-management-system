using CmsLibrary.ProjectConfiguration.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.ProjectConfiguration.BusinessLogic {
    public class ProjectsSelectionCreateValues : IProjectsSelectionCreateProcess {

        public void AllProjectsSelected( IProjectsSelectionCreateCredentials credentials) {
         
                credentials.MainProjects = true;
                credentials.SubProjects = true;
           
        }

        public void MainProjectsSelected( IProjectsSelectionCreateCredentials credentials) {
           
                credentials.MainProjects = true;
                credentials.SubProjects = false;
          
        }

        public void SubProjectsSelected( IProjectsSelectionCreateCredentials credentials ) {
         
                credentials.MainProjects = false;
                credentials.SubProjects = true;
         
        }


    }
}
