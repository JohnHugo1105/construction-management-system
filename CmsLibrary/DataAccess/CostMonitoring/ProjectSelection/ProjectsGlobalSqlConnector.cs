using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmsLibrary.Interface.CostMonitoring;

namespace CmsLibrary.DataAccess.CostMonitoring.ProjectSelection {
    public class ProjectsGlobalSqlConnector : IProjectsConnection {
        public int GetLatestIdentifier( string tableName , string events ) {
            throw new NotImplementedException( );
        }

        public void RemoveProject( IProjectsCredentials Id , string tableName , string events ) {
            throw new NotImplementedException( );
        }
    }
}
