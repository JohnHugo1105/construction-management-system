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

        public void CreateSubProject( SubProjectsModel credentials , string events , string tableName ) {
            using( SqlConnection connection = new SqlConnection(GlobalConfig.ConnString))
            {
                using( SqlCommand cmd = new SqlCommand("spProjects",connection) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@tableName" , tableName );
                    cmd.Parameters.AddWithValue( "@SubProjectName" , credentials.ProjectName );
                    cmd.Parameters.AddWithValue( "@DateStarted" , credentials.DateStarted );
                   

                    cmd.Connection.Open( );
                    //int mainProjectId = Convert.ToInt32( cmd.ExecuteScalar( ) );
                    //cmd.Parameters.AddWithValue( "@MainProjectId" , mainProjectId );
                    cmd.ExecuteNonQuery( );
                    //Console.WriteLine( mainProjectId );
                }
            }
        }

        public DataTable tableSubProjects( ) {
            throw new NotImplementedException( );
        }

        public void Update( SubProjectsModel credentials ) {
            throw new NotImplementedException( );
        }


    }
}
