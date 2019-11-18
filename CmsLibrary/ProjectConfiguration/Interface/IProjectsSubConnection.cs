using CmsLibrary.Model.CostMonitoring.ProjectSelection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring.ProjectSelection {
  public  interface IProjectsSubConnection {
        void CreateSubProject( ProjectsSubModel credentials , string events , string tableName );
        void Update( ProjectsSubModel credentials );

        DataTable tableSubProjects( string tableName );

    }
}
