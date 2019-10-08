using CmsLibrary.Model.CostMonitoring.ProjectSelection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring.ProjectSelection {
  public  interface ISubProjectsConnection : IProjectsConnection {
        void CreateMainProject( SubProjectsModel credentials , string events , string tableName );
        void Update( SubProjectsModel credentials );
    }
}
