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
using CmsLibrary.BusinessLogic.SpEvents;
using CmsLibrary.BusinessLogic.SpParameters;

namespace CmsLibrary.DataAccess.CostMonitoring {
    public class MainProjectsSqlConnector : IMainProjectsConnection {

        public void CreateMainProject( MainProjectsModel credentials , string events , string tableName ) {
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString ) )
            {
                using( SqlCommand cmd = new SqlCommand( "dbo.spProjects" , connection) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@tableName" , tableName );
                    cmd.Parameters.AddWithValue( "@ProjectName" , credentials.ProjectName );
                    cmd.Parameters.AddWithValue( "@DateStarted" , credentials.DateStarted );

                    //SqlParameter p = new SqlParameter( );
                    //p.ParameterName = "@Id";
                    //p.Size = 4;
                    //p.Direction = ParameterDirection.Output;
                    //cmd.Parameters.Add( p );

                    cmd.Connection.Open( );
                    cmd.ExecuteNonQuery( );

                    //int id = Convert.ToInt32( cmd.ExecuteScalar( ) );
                    //Console.WriteLine( id );

                }
            }
        }

        public DataTable tableMainProjects( string tableName ) {
            DataTable table = new DataTable( );

            using( SqlConnection connection =new SqlConnection(GlobalConfig.ConnString) )
            {
                using( SqlDataAdapter adapter = new SqlDataAdapter() )
                {
                    using( SqlCommand cmd = new SqlCommand( "SpProjects" , connection ) )
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue( SpProjectsParamNameList.events , SpProjectsEventsList.spGetMainProjects );
                        cmd.Parameters.AddWithValue( SpProjectsParamNameList.TableName , tableName );
                        adapter.SelectCommand = cmd;
                        adapter.Fill( table );

                        cmd.Connection.Open( );
                        cmd.ExecuteNonQuery( );

                    }
                }               
            }
            return table;

        }

        public void UpdateMainProject( MainProjectsModel credentials ) {
            throw new NotImplementedException( );
        }  


    }
}
