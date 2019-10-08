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
    public class MainProjectsSqlConnector : IMainProjectsConnection {

        public void CreateMainProject( MainProjectsModel credentials , string events , string tableName ) {
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString ) )
            {
                using( SqlCommand cmd = new SqlCommand("dbo.spProjects",connection) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                   
                    
                    SqlParameter Id = new SqlParameter( "@Id" , SqlDbType.Int );
                    Id.Direction = ParameterDirection.Output;

                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@tableName" , tableName );
                    cmd.Parameters.AddWithValue( "@ProjectName" , credentials.ProjectName );
                    cmd.Parameters.AddWithValue( "@DateStarted" , credentials.DateStarted );
                    cmd.Parameters.Add(  Id );
                    cmd.Connection.Open( );
                    cmd.ExecuteNonQuery( );

                    if( Id.Value !=DBNull.Value )
                    {
                        int pkId = Convert.ToInt32( Id.Value );
                        Console.WriteLine( pkId.ToString( ) );
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show( "Test" );
                    }
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

        public void UpdateMainProject( MainProjectsModel credentials ) {
            throw new NotImplementedException( );
        }

    }
}
