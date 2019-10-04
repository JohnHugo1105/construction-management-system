using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmsLibrary.Interface.CostMonitoring;
using CmsLibrary.Model.CostMonitoring;
using CmsLibrary.Model.CostMonitoring.ProjectSelection;
using CmsLibrary.Interface.CostMonitoring.ProjectSelection;

namespace CmsLibrary.DataAccess.CostMonitoring {
    public class SubProjectsConnector : ISubProjectsConnection {
        public void AddMainProject( SubProjectsModel credentials ) {
            throw new NotImplementedException( );
        }

        public void RemoveProject( IProjectsCredentials Id ) {
            throw new NotImplementedException( );
        }

        public DataTable tableProjects( ) {
            throw new NotImplementedException( );
        }

        public DataTable tableSubProjects( ) {
            throw new NotImplementedException( );
        }

        public void Update( SubProjectsModel credentials ) {
            throw new NotImplementedException( );
        }
    }
}
