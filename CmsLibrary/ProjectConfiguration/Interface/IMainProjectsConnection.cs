using CmsLibrary.Model.CostMonitoring;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring.ProjectSelection {

  public interface IMainProjectsConnection  {
        void CreateMainProject( MainProjectsModel credentials, string events, string tableName );
        void UpdateMainProject( MainProjectsModel credentials );

        DataTable tableMainProjects(string tableName );


    }
}
