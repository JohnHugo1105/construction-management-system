using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using MySql.Data.MySqlClient;
using System.Windows.Forms;
using GSG_Builders.RevisionedUI;
using System.Data.SqlClient;
using System.Data;
using GSG_Builders.RevisionedUI.Database_connection;

namespace GSG_Builders.RevisionedUI.Reusable_codes {
    class Archiving : Fields_reusable_base_class {

        /*  removing records on journal and moving them to archives */
        public void bill_mat_main_project_move_to_archives_main ( string project_selected ) {
            Methods_return_type mainprojid = new Methods_return_type( );
          
                using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
                {
                    using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Connection.Open( );
                        cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = bill_mat_table;
                        cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "*";
                        cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "WHERE main_project='" + project_selected + "' ";

                        SqlDataReader reader = cmd.ExecuteReader( );

                        while( reader.Read( ) )
                        {
                            //Save_record save = new Save_record( reader.GetInt32( 1 ) , reader.GetInt32( 2 ) , reader.GetString( 3 ) , reader.GetString(4 ) , reader.GetString( 5 ) , reader.GetString(7 ) , reader.GetString( 8) , reader.GetString( 9 ) , reader.GetString( 10) , reader.GetString( 11 ) , reader.GetString(12) , reader.GetString( 13 ) , Convert.ToDouble( reader.GetString(14 ) ) , reader.GetString( 15 ) , reader.GetString( 16 ) , reader.GetString( 17 ) , reader.GetString( 18 ) , reader.GetString( 19 ) , reader.GetString(6 ) );
                            //save.bill_mat_archives_save_entry_record( bill_mat_archives_main_database_table );

                        }
                        reader.Close( );
                    
                         
                        }                      
                    }               
                #region old codes
                //cmd = new MySqlCommand( );
                //cmd.Connection = Database_connection.MySQL_connection.GetConnection( );
                //cmd.Connection.Open( );
                //cmd.CommandText = "SELECT * FROM" + bill_mat_database_table + "WHERE main_project='" + project_selected + "' ";
                //MySqlDataReader reader = cmd.ExecuteReader( );

                //while( reader.Read( ) )
                //{
                //    Save_record save = new Save_record( reader.GetInt32( "main_project_id" ) , reader.GetInt32( "sub_project_id" ) , reader.GetString( "main_project" ) , reader.GetString( "sub_project" ) , reader.GetString( "date_purchased" ) , reader.GetString( "DivisionNum" ) , reader.GetString( "wbs_code" ) , reader.GetString( "material_labor" ) , reader.GetString( "main_scope_description" ) , reader.GetString( "sub_scope_description" ) , reader.GetString( "item_description" ) , reader.GetString( "unit" ) , Convert.ToDouble( reader.GetString( "quantity" ) ) , reader.GetString( "unit_cost" ) , reader.GetString( "total_amount" ) , reader.GetString( "remarks" ) , reader.GetString( "date_for_sorting" ) );
                //    save.bill_mat_archives_save_entry_record( bill_mat_archives_main_database_table );

                //}
                //reader.Close( );

                //cmd.CommandText = "DELETE FROM" + bill_mat_database_table + "WHERE main_project_id='" + mainprojid.Main_Project_Id( project_selected ) + "'  ";
                //cmd.ExecuteNonQuery( );
                #endregion
         
        }

        public void bill_mat_sub_project_move_to_archives_main ( string main_project_selected,string sub_project_selected ) {
      
        }

        public void main_sub_project_move_to_archives ( string project_selected  , string date_started, string reason_for_archiving ) {
     
          
        }
        public void sub_project_move_to_archives ( string project_selected , string date_started , string reason_for_archiving ) {
     
        }

    }
}
