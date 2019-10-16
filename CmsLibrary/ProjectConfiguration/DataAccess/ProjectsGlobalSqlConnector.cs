using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmsLibrary.Interface.CostMonitoring;
using System.Data.SqlClient;
using System.Data;
using CmsLibrary.BusinessLogic.SpParameters;
using CmsLibrary.BusinessLogic.SpEvents;

namespace CmsLibrary.DataAccess.CostMonitoring.ProjectSelection {

    /// <summary>
    /// This class is where global methods are for both Main and Sub Projects to be use
    /// </summary>
    public class ProjectsGlobalSqlConnector : IProjectsConnection {

        public int GetLatestIdentifier( string tableName  ) {
            int GetId = 0;
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString) )
            {
                using( SqlCommand cmd = new SqlCommand("spProjects",connection ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( SpProjectsParamNameList.events , SpProjectsEventsList.spLatestIdentifier );
                    cmd.Parameters.AddWithValue( SpProjectsParamNameList.TableName , tableName );

                    cmd.Connection.Open( );
                     GetId = Convert.ToInt32(cmd.ExecuteScalar( ));
                }
            }
            return GetId;
        }

        public void RemoveProject( IProjectsCredentials Id , string tableName , string events ) {
            using( SqlConnection connection = new SqlConnection(GlobalConfig.ConnString) )
            {
                using( SqlCommand cmd = new SqlCommand("spProjects",connection) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue(SpProjectsParamNameList.events, events);
                    cmd.Parameters.AddWithValue( SpProjectsParamNameList.TableName,tableName);
                    cmd.Parameters.AddWithValue(SpProjectsParamNameList.Id, Id );

                    cmd.Connection.Open( );
                    cmd.ExecuteNonQuery( );
                }
            }
        }


    }
}
