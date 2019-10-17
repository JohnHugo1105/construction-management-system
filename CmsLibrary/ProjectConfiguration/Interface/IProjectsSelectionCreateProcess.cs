using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.ProjectConfiguration.Interface {
   public interface IProjectsSelectionCreateProcess {

        void AllProjectsSelected( IProjectsSelectionCreateCredentials credentials );

        void MainProjectsSelected( IProjectsSelectionCreateCredentials credentials );

        void SubProjectsSelected( IProjectsSelectionCreateCredentials credentials );

    }
}
