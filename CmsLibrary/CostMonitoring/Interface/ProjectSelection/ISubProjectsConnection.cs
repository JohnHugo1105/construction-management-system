using CmsLibrary.Model.CostMonitoring.ProjectSelection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring.ProjectSelection {
  public  interface ISubProjectsConnection  {
        void CreateSubProject( SubProjectsModel credentials , string events , string tableName );
        void Update( SubProjectsModel credentials );

        DataTable tableSubProjects( );

    }
}
