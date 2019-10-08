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
using System.Data.SqlClient;

namespace CmsLibrary.DataAccess.CostMonitoring {
    public class SubProjectsSqlConnector : ISubProjectsConnection {
        public void CreateMainProject( SubProjectsModel credentials , string events , string tableName ) {
            using( SqlConnection connection = new SqlConnection(GlobalConfig.ConnString))
            {
                using( SqlCommand cmd = new SqlCommand("spProjects",connection) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@tableName" , tableName );
                    cmd.Parameters.AddWithValue( "@ProjectName" , credentials.ProjectName );
                    cmd.Parameters.AddWithValue( "@DateStarted" , credentials.DateStarted );

                    cmd.Connection.Open( );
                    cmd.ExecuteNonQuery( );
                }
            }
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
