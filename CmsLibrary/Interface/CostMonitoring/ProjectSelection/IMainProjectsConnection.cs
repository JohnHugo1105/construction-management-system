using CmsLibrary.Model.CostMonitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring.ProjectSelection {
    interface IMainProjectsConnection : IProjectsConnection {
        void AddMainProject( MainProjectsModel credentials );
        void Update( MainProjectsModel credentials );
    }
}
