using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.DataAccess.Login {
    /// <summary>
    /// Connection to database that disregards whatever table to interact with, this derives from ISystemConstract>IAccountLoginConnection
    /// </summary>
    public class GlobalSqlConnector : IAccountLoginConnection {

        public DataTable GetAllAccounts( string events ) {

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

        public bool IsCredentialsValid( string events , IAccountCredentials credentials ) {
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString ) )
            {
                using( SqlCommand cmd = new SqlCommand( "dbo.spLogin" , connection ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@Username" , credentials.Username );
                    cmd.Parameters.AddWithValue( "@Password" , credentials.Password );
                    cmd.Connection.Open( );
                    SqlDataReader reader = cmd.ExecuteReader( );

                    int count = 0;

                    while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull( 0 ) )
                        {
                            count++;
                        }
                    }
                    if( count == 1 )
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool IsUsernameExist( string events , IAccountCredentials credentials , string tableName ) {
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString ) )
            {
                using( SqlCommand cmd = new SqlCommand( "dbo.spLogin" , connection ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@Username" , credentials.Username );
                    cmd.Parameters.AddWithValue( "@TableName" , tableName );
                    cmd.Connection.Open( );
                    SqlDataReader reader = cmd.ExecuteReader( );

                    int count = 0;

                    while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull( 0 ) )
                        {
                            count++;
                        }
                    }
                    if( count == 1 )
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void RemoveAccount( string events , IAccountCredentials id , string tableName ) {
            using( SqlConnection connection = new SqlConnection( GlobalConfig.ConnString ) )
            {
                using( SqlCommand cmd = new SqlCommand( "dbo.spLogin" , connection ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue( "@event" , events );
                    cmd.Parameters.AddWithValue( "@TableName" , tableName );
                    cmd.Parameters.AddWithValue( "@Id" , id.Id );

                    cmd.Connection.Open( );
                    cmd.ExecuteNonQuery( );
                }
            }
        }


    }
}
