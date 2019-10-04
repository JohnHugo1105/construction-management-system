using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GSG_Builders.RevisionedUI.Database_connection;
//using MySql.Data.MySqlClient;
using System.Windows.Forms;
using MetroFramework.Controls;
using System.Data.SqlClient;
using System.Data;
//using MySql.Data.MySqlClient;

namespace GSG_Builders.RevisionedUI.Reusable_codes {
    class Computation : Fields_reusable_base_class{

        public Computation() {

        }

        public Computation(TextBox _quantity_textfield, MetroTextBox _unitcost_textfield, MetroTextBox _total_amount__textfield) {
            quantity_txt = _quantity_textfield;
            unit_cost_metroTextBox = _unitcost_textfield;
            total_amount_metroTextBox = _total_amount__textfield;
        }
        public Computation( TextBox _quantity_textfield , TextBox _unitcost_textfield , TextBox _total_amount__textfield ) {
            quantity_text = _quantity_textfield;
            unit_cost_text = _unitcost_textfield;
            total_amount_text = _total_amount__textfield;
        }
        public void  compute_total_amount() {
            total_amount_metroTextBox.Text = "0.00";
           quantity_valueholder = (string.IsNullOrEmpty(quantity_txt.Text) ? (double?)null : Convert.ToDouble(quantity_txt.Text));
           unitcost_valueholder = (string.IsNullOrEmpty(unit_cost_metroTextBox.Text) ? (double?)null : Convert.ToDouble(unit_cost_metroTextBox.Text));
           total_amount_valueholder = (string.IsNullOrEmpty(total_amount_metroTextBox.Text) ? (double?)null : Convert.ToDouble(total_amount_metroTextBox.Text));
           
            unit_cost_metroTextBox.Text = Convert.ToDouble(unitcost_valueholder).ToString("#,##0.00");
            total_amount_valueholder = quantity_valueholder * unitcost_valueholder;
            total_amount_metroTextBox.Text = Convert.ToDouble(total_amount_valueholder).ToString("#,##0.00");
        }
     
        public void compute_vat_vat_exclusive(MetroTextBox total_amount_metroTextBox , MetroTextBox vat_exclusive_metroTextBox, MetroTextBox vat_metroTextBox ) {
            vat_exclusive_metroTextBox.Text = "0.00";
            vat_metroTextBox.Text = "0.00";            

            quantity_valueholder = ( string.IsNullOrEmpty( total_amount_metroTextBox.Text ) ? ( double? ) null : Convert.ToDouble( total_amount_metroTextBox.Text ) );
            unitcost_valueholder = ( string.IsNullOrEmpty( vat_exclusive_metroTextBox.Text ) ? ( double? ) null : Convert.ToDouble( vat_exclusive_metroTextBox.Text ) );
            total_amount_valueholder = ( string.IsNullOrEmpty( vat_metroTextBox.Text ) ? ( double? ) null : Convert.ToDouble( vat_metroTextBox.Text ) );
          //  vat_value = ( string.IsNullOrEmpty( vat_metroTextBox.Text ) ? ( double? ) null : Convert.ToDouble( vat_metroTextBox.Text ) );

            // unit_cost_metroTextBox.Text = Convert.ToDouble( unitcost_valueholder ).ToString( "#,##0.00" );
            unitcost_valueholder = quantity_valueholder / 1.12;
            vat_exclusive_metroTextBox.Text = Convert.ToDouble( unitcost_valueholder ).ToString( "#,##0.00" );
            total_amount_valueholder = quantity_valueholder - unitcost_valueholder;
            vat_metroTextBox.Text = Convert.ToDouble( total_amount_valueholder ).ToString( "#,##0.00" );
        }

        public void compute_total_amount_warehouse_inventory_new_entry( ) {
          //  total_amount_text.Text = "0.00";
            quantity_valueholder = ( string.IsNullOrEmpty( quantity_text.Text ) ? ( double? ) null : Convert.ToDouble( quantity_text.Text ) );
            unitcost_valueholder = ( string.IsNullOrEmpty( unit_cost_text.Text ) ? ( double? ) null : Convert.ToDouble( unit_cost_text.Text ) );
            //total_amount_valueholder = ( string.IsNullOrEmpty( total_amount_text.Text ) ? ( double? ) null : Convert.ToDouble( total_amount_text.Text ) );

            unit_cost_text.Text = Convert.ToDouble( unitcost_valueholder ).ToString( "#,##0.00" );
            total_amount_valueholder = quantity_valueholder * unitcost_valueholder;
            total_amount_text.Text = Convert.ToDouble( total_amount_valueholder ).ToString( "#,##0.00" );
        }

        public string compute_total_amount_warehouse_inventory_edit_records(string quantity_text, string unit_cost_text, string total_amount_text ) {
         //   total_amount_text = "0.00";
            quantity_valueholder = ( string.IsNullOrEmpty( quantity_text ) ? ( double? ) null : Convert.ToDouble( quantity_text ) );
            unitcost_valueholder = ( string.IsNullOrEmpty( unit_cost_text ) ? ( double? ) null : Convert.ToDouble( unit_cost_text ) );
            //total_amount_valueholder = ( string.IsNullOrEmpty( total_amount_text ) ? ( double? ) null : Convert.ToDouble( total_amount_text) );

          //  unit_cost_text = Convert.ToDouble( unitcost_valueholder ).ToString( "#,##0.00" );
            total_amount_valueholder = quantity_valueholder * unitcost_valueholder;
            
            return total_amount_text = Convert.ToDouble( total_amount_valueholder ).ToString( "#,##0.00" );
        }

        public string compute_total_amount (string project_selected, string table_name ) {
            string value_holder = "";
            double val = 0;

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = table_name;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "total_amount";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "WHERE main_project='" + project_selected.Replace( "'" , "''" ) + "' ";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        value_holder = reader.GetString( 0 ).Replace( "," , "" );
                        val += Convert.ToDouble( value_holder );
                    }
                    return Convert.ToDouble( val ).ToString( "#,##0.00" );
                }
            }
            #region old codes
            //cmd = new MySqlCommand( );
            //cmd.Connection = MySQL_connection.GetConnection( );
            //cmd.Connection.Open( );
            //cmd.CommandText = "SELECT total_amount FROM"+bill_mat_database_table+"WHERE main_project='"+project_selected.Replace("'","''")+"' ";
            //MySqlDataReader reader = cmd.ExecuteReader( );
            #endregion
        }

        public string compute_vat_exclusive( string project_selected, string table_name ) {
            string value_holder = "";
            double val = 0;

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = table_name;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "vat_exclusive";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "WHERE main_project='" + project_selected.Replace( "'" , "''" ) + "' ";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        value_holder = reader.GetString( 0 ).Replace( "," , "" );
                        val += Convert.ToDouble( value_holder );
                    }
                    return Convert.ToDouble( val ).ToString( "#,##0.00" );
                }
            }
            #region old codes
            //cmd = new MySqlCommand( );
            //cmd.Connection = MySQL_connection.GetConnection( );
            //cmd.Connection.Open( );
            //cmd.CommandText = "SELECT total_amount FROM"+bill_mat_database_table+"WHERE main_project='"+project_selected.Replace("'","''")+"' ";
            //MySqlDataReader reader = cmd.ExecuteReader( );
            #endregion
        }
        public string compute_vat( string project_selected, string table_name ) {
            string value_holder = "";
            double val = 0;

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.stored_proc_all , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.AddWithValue( event_parameter ,select_event);
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = table_name;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "vat";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "WHERE main_project='" + project_selected.Replace( "'" , "''" ) + "' ";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        value_holder = reader.GetString( 0 ).Replace( "," , "" );
                        val += Convert.ToDouble( value_holder );
                    }
                    return Convert.ToDouble( val ).ToString( "#,##0.00" );
                }
            }
        }
        public string journal_compute_total_expense( string project_selected ) {
            string value_holder = "";
            double val = 0;

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = journal_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "total_amount";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "WHERE main_project='" + project_selected.Replace( "'" , "''" ) + "' ";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        value_holder = reader.GetString( 0 ).Replace( "," , "" );
                        val += Convert.ToDouble( value_holder );
                    }
                    return Convert.ToDouble( val ).ToString( "#,##0.00" );
                }
            }
            #region old codes
            //cmd = new MySqlCommand( );
            //cmd.Connection = MySQL_connection.GetConnection( );
            //cmd.Connection.Open( );
            //cmd.CommandText = "SELECT total_amount FROM" + journal_database_table+"WHERE expense_revenue='expense' && main_project='"+ project_selected.Replace( "'", "''" ) + "' ";
            //MySqlDataReader reader = cmd.ExecuteReader( );
            //while( reader.Read( ) )
            //{
            //    value_holder = reader.GetString( 0 ).Replace( "," , "" );
            //    val += Convert.ToDouble( value_holder );
            //}          
            //return Convert.ToDouble( val ).ToString( "#,##0.00" );
            #endregion
        }

        public  string warehouse_inventory_out_amount(MetroRadioButton with_amount_metroRadioButton, string unit_cost, string quantity ) {
            quantity_valueholder = ( string.IsNullOrEmpty( quantity ) ? ( double? ) null : Convert.ToDouble( quantity ) );
            unitcost_valueholder = ( string.IsNullOrEmpty( unit_cost ) ? ( double? ) null : Convert.ToDouble( unit_cost ) );
          //  total_amount_valueholder = ( string.IsNullOrEmpty( total_amount_metroTextBox.Text ) ? ( double? ) null : Convert.ToDouble( total_amount_metroTextBox.Text ) );

       //     unit_cost_metroTextBox.Text = Convert.ToDouble( unitcost_valueholder ).ToString( "#,##0.00" );
       //     total_amount_valueholder = quantity_valueholder * unitcost_valueholder;
        //    total_amount_metroTextBox.Text = Convert.ToDouble( total_amount_valueholder ).ToString( "#,##0.00" );

            // string value_holder = "";
            double? val = 0;
            if( with_amount_metroRadioButton.Checked )
            {
                val = unitcost_valueholder * quantity_valueholder;
            }
            return Convert.ToDouble( val ).ToString( "#,##0.00" );
        }
    }
}
