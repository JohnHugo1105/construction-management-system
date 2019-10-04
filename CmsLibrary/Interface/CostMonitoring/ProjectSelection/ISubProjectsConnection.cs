using CmsLibrary.Model.CostMonitoring.ProjectSelection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring.ProjectSelection {
    interface ISubProjectsConnection : IProjectsConnection {
        void AddMainProject( SubProjectsModel credentials );
        void Update( SubProjectsModel credentials );
    }
}
