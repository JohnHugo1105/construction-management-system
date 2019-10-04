using CmsLibrary.Interface.Login;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace CmsLibrary.DataAccess.Login {
    public class AdminSqlConnector : IAdminLoginConnection {
        public DataTable AdminGetAccounts( string events ) {
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString ) )
            {
                using( SqlDataAdapter adapter = new SqlDataAdapter( ) )
                {
                    using( SqlCommand cmd = new SqlCommand( "dbo.spLogin" , connection ) )
                    {
                        DataTable table = new DataTable( );
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue( "@event" , events );
                        adapter.SelectCommand = cmd;
                        adapter.Fill( table );

                        cmd.Connection.Open( );
                        cmd.ExecuteNonQuery( );
                        return table;
                    }
                }
            }
        }

        public void Create( string events , AdminModel credentials , string tableName ) {
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString ) )
            {
                using( SqlCommand cmd = new SqlCommand( "dbo.spLogin" , connection ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@TableName" , tableName );
                    cmd.Parameters.AddWithValue( "@Username" , credentials.Username );
                    cmd.Parameters.AddWithValue( "@Password" , credentials.Password );
                    cmd.Parameters.AddWithValue( "@Type" , credentials.Type );
                    cmd.Parameters.AddWithValue( "@LoginAccessCode" , credentials.LoginAccessCode );

                    cmd.Connection.Open( );
                    cmd.ExecuteNonQuery( );
                }
            }
        }     

        public void Update( string events , AdminModel credentials , string tableName ) {
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString ) )
            {
                using( SqlCommand cmd = new SqlCommand( "dbo.spLogin" , connection ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@TableName" , tableName );
                    cmd.Parameters.AddWithValue( "@Username" , credentials.Username );
                    cmd.Parameters.AddWithValue( "@Password" , credentials.Password );
                    cmd.Parameters.AddWithValue( "@Type" , credentials.Type );
                    cmd.Parameters.AddWithValue( "@LoginAccessCode" , credentials.LoginAccessCode );
                    cmd.Parameters.AddWithValue( "@Id" , credentials.Id );

                    cmd.Connection.Open( );
                    cmd.ExecuteNonQuery( );
                }
            }
        }


    }
}
