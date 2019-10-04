using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using GSG_Builders.RevisionedUI.Cost_monitoring.Reports.Dashboard;
using GSG_Builders.RevisionedUI.Database_connection;
using GSG_Builders.RevisionedUI.Reusable_codes;
//using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.Reports.Dashboard {
    public partial class Dashboard_form : MetroFramework.Forms.MetroForm {
        public static string Project_price = "";
        public Dashboard_form( ) {
            InitializeComponent( );            
        }
        public string print_decision_table_name( ) {
            string choice = "";
            if( Bill_Mat.BillMat_Form.print_bill_mat==true )
            {
                choice = Fields_reusable_base_class.bill_mat_table;
            }
            else if( Journal.Journal_form.print_journal==true )
            {
                choice = Fields_reusable_base_class.journal_table;
            }
            return choice;
        }
        public string print_decision_project_name( ) {
            string project_name = "";
            if( Bill_Mat.BillMat_Form.print_bill_mat == true )
            {
                project_name = Bill_Mat.BillMat_Form.project_selected_str;
            }
            else if( Journal.Journal_form.print_journal == true )
            {
                project_name = Journal.Journal_form.project_selected_str;
            }
            return project_name;
        }
        public string with_vat( ) {
            Record_count count = new Record_count( );
            string vat = "";
            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.stored_proc_all , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.AddWithValue( Fields_reusable_base_class.event_parameter , Fields_reusable_base_class.select_event );
                    cmd.Parameters.AddWithValue( Fields_reusable_base_class.table_parameter , print_decision_table_name() );
                    cmd.Parameters.AddWithValue( Fields_reusable_base_class.col_select , "vat_exclusive"  );
                    cmd.Parameters.AddWithValue( Fields_reusable_base_class.other_query , "WHERE main_project='"+ print_decision_project_name() + "' " );
                    SqlDataReader reader = cmd.ExecuteReader( );
                    double amount = 0;
                    while( reader.Read() )
                    {
                        if( !reader.IsDBNull(0) )
                        {
                            amount +=Convert.ToDouble( reader.GetString(0).Replace(",",""));
                        }                        
                    }
                    if( amount>0 )
                    {
                        vat = "Vat exclusive";
                    }
                    else
                    {
                        vat = "";
                    }
                }
            }       
            return vat;
        }
        private void crystalReportViewer1_Load( object sender , EventArgs e ) {
         
        }
     
        private void Dashboard_form_Load( object sender , EventArgs e ) {
            DataTable main_table = new DataTable( );
            main_table.Columns.Add( "division_num" );
            main_table.Columns.Add( "wbs_code" );
            main_table.Columns.Add( "main_scope_description" );
            main_table.Columns.Add( "sub_scope_description" );
            main_table.Columns.Add( "budget_cost" );
            main_table.Columns.Add( "actual_cost" );
            main_table.Columns.Add( "variance" );

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand(  ) )
                {
                    cmd.CommandText = "SELECT division_num,main_scope_description,wbs_code, sub_scope_description, total_amount FROM bill_mat GROUP BY division_num,main_scope_description,wbs_code, sub_scope_description, total_amount UNION SELECT division_num,main_scope_description,wbs_code, sub_scope_description, total_amount FROM journal GROUP BY division_num, main_scope_description, wbs_code, sub_scope_description, total_amount";
                    cmd.Connection = con;
                    cmd.Connection.Open( );
                    //cmd.Parameters.AddWithValue(Fields_reusable_base_class.event_parameter , Fields_reusable_base_class.select_event );
                    //cmd.Parameters.AddWithValue( Fields_reusable_base_class.table_parameter , Fields_reusable_base_class.bill_mat_table+ " GROUP BY division_num,wbs_code,main_scope_description,sub_scope_description, total_amount " + "UNION SELECT division_num,wbs_code,main_scope_description,sub_scope_description FROM "+Fields_reusable_base_class.journal_table+" "+ "GROUP BY division_num,wbs_code,main_scope_description,sub_scope_description, total_amount" );
                    //cmd.Parameters.AddWithValue( Fields_reusable_base_class.col_select , "division_num , wbs_code , main_scope_description , sub_scope_description" );
                    //cmd.Parameters.AddWithValue( Fields_reusable_base_class.other_query ,  );
                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read() )
                    {
                        if( !reader.IsDBNull(0) )
                        {
                            MessageBox.Show( reader.GetString(0) );
                        }
                    }
                }
            }

            #region break_down_report
            //DataTable variance_report_DataTable = new DataTable( );
            //variance_report_DataTable.Columns.Add( "Main scope description bm" );
            //variance_report_DataTable.Columns.Add( "Sub scope description bm" );
            //variance_report_DataTable.Columns.Add( "Item description bm" );
            //variance_report_DataTable.Columns.Add( "Unit bm" );
            //variance_report_DataTable.Columns.Add( "Quantity bm" );
            //variance_report_DataTable.Columns.Add( "Unit cost bm" );
            //variance_report_DataTable.Columns.Add( "Total amount bm" );

            //variance_report_DataTable.Columns.Add( "Main scope description j" );
            //variance_report_DataTable.Columns.Add( "Sub scope description j" );
            //variance_report_DataTable.Columns.Add( "Item description j" );
            //variance_report_DataTable.Columns.Add( "Unit j" );
            //variance_report_DataTable.Columns.Add( "Quantity j" );
            //variance_report_DataTable.Columns.Add( "Avg unit cost j" );
            //variance_report_DataTable.Columns.Add( "Total amount j" );

            //variance_report_DataTable.Columns.Add( "Quantity v" );
            //variance_report_DataTable.Columns.Add( "Unit v" );
            //variance_report_DataTable.Columns.Add( "Unit cost v" );
            //variance_report_DataTable.Columns.Add( "Variance" );

            ////bill_mat
            //DataTable main_scope_description_ALL_table = new DataTable( );
            //main_scope_description_ALL_table.Columns.Add( "Main scope description bm" );
            ////main_scope_description_bill_mat_table.Columns.Add( "Sub scope description bm" );
            //DataTable sub_scope_description_bill_mat_table = new DataTable( );
            //sub_scope_description_bill_mat_table.Columns.Add( "Sub scope description bm" );
            //DataTable item_description_ALL_table = new DataTable( );
            //item_description_ALL_table.Columns.Add( "Item description bm" );

            //DataTable quantity_table_bill_mat = new DataTable( );
            //quantity_table_bill_mat.Columns.Add( "Quantity bm" );
            //quantity_table_bill_mat.Columns.Add( "Item description bm" );

            //DataTable unit_cost_table_bill_mat = new DataTable( );
            //unit_cost_table_bill_mat.Columns.Add( "Unit cost bm" );
            //unit_cost_table_bill_mat.Columns.Add( "Item description bm" );

            //DataTable unit_ALL_table = new DataTable( );
            //unit_ALL_table.Columns.Add( "Unit bm" );

            ////journal
            //DataTable main_scope_description_journal_table = new DataTable( );
            //main_scope_description_journal_table.Columns.Add( "Main scope description j" );
            //DataTable sub_scope_description_journal_table = new DataTable( );
            //sub_scope_description_journal_table.Columns.Add( "Sub scope description j" );
            //DataTable item_description_journal_table = new DataTable( );
            //item_description_journal_table.Columns.Add( "Item description j" );

            //DataTable quantity_table_journal = new DataTable( );
            //quantity_table_journal.Columns.Add( "Quantity j" );
            //quantity_table_journal.Columns.Add( "Item description j" );

            //DataTable unit_cost_table_journal = new DataTable( );
            //unit_cost_table_journal.Columns.Add( "Avg unit cost j" );
            //unit_cost_table_journal.Columns.Add( "Item description j" );

            ////total_amount bill_mat
            //DataTable total_amount_bill_mat_table = new DataTable( );
            //total_amount_bill_mat_table.Columns.Add( "Total amount bm" );
            //total_amount_bill_mat_table.Columns.Add( "Main scope description bm" );
            //total_amount_bill_mat_table.Columns.Add( "Sub scope description bm" );
            //total_amount_bill_mat_table.Columns.Add( "Item description total" );

            ////journal
            //DataTable total_amount_journal_table = new DataTable( );
            //total_amount_journal_table.Columns.Add( "Total amount j" );
            //total_amount_journal_table.Columns.Add( "Main scope description j" );
            //total_amount_journal_table.Columns.Add( "Sub scope description j" );
            //total_amount_journal_table.Columns.Add( "Item description total" );

            ///* this will get the count of all records group by item description */
            //MySqlConnection con = Database_connection.MYSQL_stored_proc_connection.GetConnection( );
            //MySqlCommand cmd = new MySqlCommand( @"group_by_item_description_ALL" , con );
            //cmd.Connection.Open( );
            //cmd.CommandType = CommandType.StoredProcedure;

            //MySqlDataReader reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    variance_report_DataTable.Rows.Add( string.Empty );
            //}
            //reader.Close( );
            ///* ends here */

            ///*this is to get the group by main scope descriptions of bill mat and adding it to the Data table table_all */
            //cmd = new MySqlCommand( @"group_by_main_scope_description_ALL" , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    main_scope_description_ALL_table.Rows.Add( reader.GetString( 0 ) );
            //}
            //reader.Close( );
            ///* ends here */


            ///* this is to get the group by main scope descriptions of bill mat and adding it to the Data table table_all */
            //cmd = new MySqlCommand( @"group_by_sub_scope_description_ALL " , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    sub_scope_description_bill_mat_table.Rows.Add( reader.GetString( 0 ) );
            //}
            //reader.Close( );
            ///* ends here */


            ///* this is to get the group by main scope descriptions of bill mat and adding it to the Data table table_all */
            //cmd = new MySqlCommand( @"group_by_item_description_ALL " , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    item_description_ALL_table.Rows.Add( reader.GetString( 0 ) );
            //}
            //reader.Close( );
            ///*ends here*/

            ///* this is to get the group by main scope descriptions of bill mat and adding it to the Data table table_all */
            //cmd = new MySqlCommand( @"group_by_item_description_ALL " , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    unit_ALL_table.Rows.Add( reader.GetString( "unit" ) );
            //}
            //reader.Close( );
            ///*ends here*/

            //cmd = new MySqlCommand( @"quantity_bill_mat" , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    quantity_table_bill_mat.Rows.Add( reader.GetString( 0 ) , reader.GetString( 1 ) );
            //}
            //reader.Close( );
            ///*ends here*/

            //cmd = new MySqlCommand( @"quantity_journal" , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    quantity_table_journal.Rows.Add( reader.GetString( 0 ) , reader.GetString( 1 ) );
            //}
            //reader.Close( );
            ///*ends here*/

            //cmd = new MySqlCommand( @"unit_cost_bill_mat" , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    unit_cost_table_bill_mat.Rows.Add( reader.GetString( 0 ) , reader.GetString( 1 ) );
            //}
            //reader.Close( );
            ///*ends here*/

            //cmd = new MySqlCommand( @"unit_cost_journal" , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    unit_cost_table_journal.Rows.Add( reader.GetString( 0 ) , reader.GetString( 1 ) );
            //}
            //reader.Close( );
            ///*ends here*/


            //cmd = new MySqlCommand( @"total_amount_bill_mat" , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    total_amount_bill_mat_table.Rows.Add( reader.GetString( 0 ) , reader.GetString( 1 ) , reader.GetString( 2 ) , reader.GetString( 3 ) );
            //}
            //reader.Close( );
            ///*ends here*/

            //cmd = new MySqlCommand( @"total_amount_journal" , con );
            //cmd.CommandType = CommandType.StoredProcedure;
            //reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    total_amount_journal_table.Rows.Add( reader.GetString( 0 ) , reader.GetString( 1 ) , reader.GetString( 2 ) , reader.GetString( 3 ) );
            //}
            //reader.Close( );
            ///*ends here*/

            //////////////////////////////////////

            //foreach( DataRow row in main_scope_description_ALL_table.Rows )
            //{
            //    foreach( DataRow row_journal in sub_scope_description_bill_mat_table.Rows )
            //    {
            //        variance_report_DataTable.Rows[ row.Table.Rows.IndexOf( row ) ][ "Main scope description bm" ] = row[ "Main scope description bm" ].ToString( );
            //    }
            //}

            //foreach( DataRow row in sub_scope_description_bill_mat_table.Rows )
            //{
            //    variance_report_DataTable.Rows[ row.Table.Rows.IndexOf( row ) ][ "Sub scope description bm" ] = row[ "Sub scope description bm" ].ToString( );
            //}

            //foreach( DataRow row in item_description_ALL_table.Rows )
            //{
            //    variance_report_DataTable.Rows[ row.Table.Rows.IndexOf( row ) ][ "Item description bm" ] = row[ "Item description bm" ].ToString( );
            //}

            //foreach( DataRow row in unit_ALL_table.Rows )
            //{
            //    variance_report_DataTable.Rows[ row.Table.Rows.IndexOf( row ) ][ "Unit bm" ] = row[ "Unit bm" ].ToString( );
            //}

            //foreach( DataRow table_all_row in item_description_ALL_table.Rows )
            //{
            //    foreach( DataRow row in quantity_table_bill_mat.Rows )
            //    {
            //        if( row[ "Item description bm" ].ToString( ) == table_all_row[ "Item description bm" ].ToString( ) )
            //        {
            //            variance_report_DataTable.Rows[ table_all_row.Table.Rows.IndexOf( table_all_row ) ][ "Quantity bm" ] = row[ "Quantity bm" ].ToString( );
            //        }
            //    }
            //}

            //foreach( DataRow table_all_row in item_description_ALL_table.Rows )
            //{
            //    foreach( DataRow row in quantity_table_journal.Rows )
            //    {
            //        if( row[ "Item description j" ].ToString( ) == table_all_row[ "Item description bm" ].ToString( ) )
            //        {
            //            variance_report_DataTable.Rows[ table_all_row.Table.Rows.IndexOf( table_all_row ) ][ "Quantity j" ] = row[ "Quantity j" ].ToString( );
            //        }
            //    }
            //}

            //foreach( DataRow table_all_row in item_description_ALL_table.Rows )
            //{
            //    foreach( DataRow row in unit_cost_table_bill_mat.Rows )
            //    {
            //        if( row[ "Item description bm" ].ToString( ) == table_all_row[ "Item description bm" ].ToString( ) )
            //        {
            //            variance_report_DataTable.Rows[ table_all_row.Table.Rows.IndexOf( table_all_row ) ][ "Unit cost bm" ] = row[ "Unit cost bm" ].ToString( );
            //        }
            //    }
            //}

            //foreach( DataRow table_all_row in item_description_ALL_table.Rows )
            //{
            //    foreach( DataRow row in unit_cost_table_journal.Rows )
            //    {
            //        if( row[ "Item description j" ].ToString( ) == table_all_row[ "Item description bm" ].ToString( ) )
            //        {
            //            variance_report_DataTable.Rows[ table_all_row.Table.Rows.IndexOf( table_all_row ) ][ "Avg unit cost j" ] = row[ "Avg unit cost j" ].ToString( );
            //        }
            //    }
            //}

            ////Budget
            //double result = 0;


            //foreach( DataRow qty_bm_row in quantity_table_bill_mat.Rows )
            //{
            //    foreach( DataRow uc_bm_row in unit_cost_table_bill_mat.Rows )
            //    {
            //        foreach( DataRow table_all_row in variance_report_DataTable.Rows )
            //        {
            //            if( qty_bm_row[ "Item description bm" ].ToString( ) == table_all_row[ "Item description bm" ].ToString( ) && uc_bm_row[ "Item description bm" ].ToString( ) == table_all_row[ "Item description bm" ].ToString( ) )
            //            {
            //                result = Convert.ToDouble( qty_bm_row[ "Quantity bm" ].ToString( ) ) * Convert.ToDouble( uc_bm_row[ "Unit cost bm" ].ToString( ) );
            //                variance_report_DataTable.Rows[ variance_report_DataTable.Rows.IndexOf( table_all_row ) ][ "Total amount bm" ] = result.ToString( "#,##0.00" );
            //                //MessageBox.Show( qty_bm_row[ "Quantity bm" ].ToString( )+"---"+ uc_bm_row[ "Unit cost bm" ].ToString( ) + "---" + table_all_row[ "Item description bm" ].ToString( ) );
            //            }
            //        }
            //    }
            //}
            ////ends here

            ////journal
            //double result2 = 0;

            //foreach( DataRow qty_j_row in quantity_table_journal.Rows )
            //{
            //    foreach( DataRow uc_j_row in unit_cost_table_journal.Rows )
            //    {
            //        foreach( DataRow table_all_row in variance_report_DataTable.Rows )
            //        {
            //            if( qty_j_row[ "Item description j" ].ToString( ) == table_all_row[ "Item description bm" ].ToString( ) && uc_j_row[ "Item description j" ].ToString( ) == table_all_row[ "Item description bm" ].ToString( ) )
            //            {
            //                result2 = Convert.ToDouble( qty_j_row[ "Quantity j" ].ToString( ) ) * Convert.ToDouble( uc_j_row[ "Avg Unit cost j" ].ToString( ) );
            //                variance_report_DataTable.Rows[ variance_report_DataTable.Rows.IndexOf( table_all_row ) ][ "Total amount j" ] = result2.ToString( "#,##0.00" );
            //            }
            //        }
            //    }
            //}
            //ends here

            #endregion

            //   DataTable main_table = new DataTable( );
            //   main_table.Columns.Add( "main_scope_description" );
            //   //  main_table.Columns.Add( "total_amount" );

            //   DataTable main_scope_budget = new DataTable( );
            //   main_scope_budget.Columns.Add( "Main scope description" );
            //    main_scope_budget.Columns.Add( "Main scope description total amount budget" );

            //   DataTable sub_scope_budget = new DataTable( );
            //   sub_scope_budget.Columns.Add( "Sub scope description" );
            //   DataTable item_budget = new DataTable( );
            //   item_budget.Columns.Add( "item description" );

            //   DataTable main_scope_budget_total_amount = new DataTable( );
            //   main_scope_budget_total_amount.Columns.Add( "Main scope description total amount" );
            //   main_scope_budget_total_amount.Columns.Add( "Main scope description" );

            //   DataTable sub_scope_budget_total_amount = new DataTable( );
            //   sub_scope_budget_total_amount.Columns.Add( "Sub scope description total amount" );
            //   DataTable item_budget_total_amount = new DataTable( );
            //   item_budget_total_amount.Columns.Add( "item description total amount" );

            //   DataTable quantity_budget = new DataTable( );
            //   quantity_budget.Columns.Add( "quantity" );
            //   DataTable avg_unit_cost_budget = new DataTable( );
            //   avg_unit_cost_budget.Columns.Add( "avg unit cost" );


            //   //grand_total_DataTable.Columns.Add( "item description" );
            //   //grand_total_DataTable.Columns.Add( "Main scope description total amount actual" );
            //   //grand_total_DataTable.Columns.Add( "Main scope description total amount budget" );
            //   //grand_total_DataTable.Columns.Add( "Sub scope description total amount actual" );
            //   //grand_total_DataTable.Columns.Add( "Sub scope description total amount budget" );
            //   //grand_total_DataTable.Columns.Add( "item description total amount actual" );
            //   //grand_total_DataTable.Columns.Add( "item description total amount budget" );
            //   //grand_total_DataTable.Columns.Add( "total quantity actual" );

            //   cmd = new MySqlCommand( @"budget_main_scope_description" , con );
            //   cmd.CommandType = CommandType.StoredProcedure;
            //   reader = cmd.ExecuteReader( );

            //   while( reader.Read( ) )
            //   {
            //       // MessageBox.Show( reader.GetString( 0 )+"--"+ reader.GetString( 1 ) );
            //       main_scope_budget.Rows.Add( reader.GetString( 0 ) ,Convert.ToDouble( reader.GetString( 1 )).ToString( "#,##0.00" ) );
            //   }
            ////   main_scope_budget.Rows.Add("Sub-scope","" );
            //   reader.Close( );

            //   cmd = new MySqlCommand( @" budget_sub_scope_description_total_amount" , con );
            //   cmd.CommandType = CommandType.StoredProcedure;
            //   reader = cmd.ExecuteReader( );

            //   while( reader.Read( ) )
            //   {
            //       sub_scope_budget.Rows.Add( reader.GetString( 0 )/* , Convert.ToDouble( reader.GetString( 1 ) ).ToString( "#,##0.00" )*/ );
            //   }
            //   reader.Close( );

            //   //cmd = new MySqlCommand( @"budget_item_description_total_amount" , con );
            //   //cmd.CommandType = CommandType.StoredProcedure;
            //   //reader = cmd.ExecuteReader( );

            //   //while( reader.Read( ) )
            //   //{
            //   //    main_scope_budget.Rows.Add( reader.GetString( 0 ) , Convert.ToDouble( reader.GetString( 1 ) ).ToString( "#,##0.00" ) );
            //   //}
            //   //reader.Close( );


            //   //double amount = 0;
            //   //foreach( DataRow main_scope_budget_row in main_scope_budget.Rows )
            //   //{
            //   //    foreach( DataRow main_scope_budget_total_amount_row in main_scope_budget_total_amount.Rows )
            //   //    {
            //   //        if( main_scope_budget_row[ "Main scope description" ].ToString( ) == main_scope_budget_total_amount_row[ "Main scope description" ].ToString( ) )
            //   //        {
            //   //            //MessageBox.Show( main_scope_budget_row[ "Main scope description" ].ToString( ) + "==" + main_scope_budget_total_amount_row[ "Main scope description" ].ToString( ) );
            //   //            amount = Convert.ToDouble( main_scope_budget_total_amount_row[ "Main scope description total amount" ].ToString( ) );
            //   //            MessageBox.Show( amount.ToString( ) );
            //   //        }
            //   //    }
            //   //}

            #region budget & actual report
            /* this will get the count of all records group by item description */
            //////using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            //////{
            //////    using( SqlCommand cmd = new SqlCommand( stored_procedures_list.stored_proc_all , con ) )
            //////    {
            //////        cmd.CommandType = CommandType.StoredProcedure;
            //////        cmd.Connection.Open( );
            //////        cmd.Parameters.AddWithValue( Fields_reusable_base_class.event_parameter , Fields_reusable_base_class.select_event );
            //////        cmd.Parameters.AddWithValue( Fields_reusable_base_class.table_parameter , print_decision_table_name( ) );
            //////        cmd.Parameters.AddWithValue( Fields_reusable_base_class.col_select , "*" );
            //////        cmd.Parameters.AddWithValue( Fields_reusable_base_class.other_query , "WHERE main_project='" + print_decision_project_name() + "' " );
            //////        SqlDataAdapter adapter = new SqlDataAdapter( );
            //////        adapter.SelectCommand = cmd;
            //////        DataTable table = new DataTable( );
            //////        adapter.Fill( table );

            //////        if( Bill_Mat.BillMat_Form.print_bill_mat == true )
            //////        {
            //////            budget_print_report var = new budget_print_report( );

            //////            var.Database.Tables[ print_decision_table_name( ) ].SetDataSource( table );
            //////            var.SetParameterValue( "project name" , print_decision_project_name( ) );
            //////            var.SetParameterValue( "vat exclusive parameter" , with_vat( ) );
            //////            var.SetParameterValue( "project price" , Project_price );
            //////            crystalReportViewer1.ReportSource = null;
            //////            crystalReportViewer1.ReportSource = var;
            //////        }
            //////        else if( Journal.Journal_form.print_journal == true )
            //////        {
            //////            actual_report var = new actual_report( );

            //////            var.Database.Tables[ print_decision_table_name( )+"1" ].SetDataSource( table );
            //////            var.SetParameterValue( "project name" , print_decision_project_name( ) );
            //////            var.SetParameterValue( "vat exclusive parameter" , with_vat() );
            //////            var.SetParameterValue( "project price" , Project_price );
            //////            crystalReportViewer1.ReportSource = null;
            //////            crystalReportViewer1.ReportSource = var;
            //////        }

            //     //////var.Database.Tables[ "grand_total_DataTable" ].SetDataSource( main_scope_budget );
            //      //////var.Database.Tables[ "DataTable1" ].SetDataSource( sub_scope_budget );          

            //var.Database.Tables[ print_decision_table_name() ].SetDataSource( bill_mat_table );
            //var.SetParameterValue( "project name" , print_decision_project_name() );
            //crystalReportViewer1.ReportSource = null;
            //crystalReportViewer1.ReportSource = var;
            //////    }
            //////}
            #endregion

            #region old codes
            //MySqlConnection con = MYSQL_stored_proc_connection.GetConnection( );
            //MySqlCommand cmd = new MySqlCommand( @"load_bill_mat" , con );
            //cmd.Connection.Open( );
            //cmd.CommandType = CommandType.StoredProcedure;
            //cmd.Parameters.Add( @"project" , MySqlDbType.VarChar ).Value = Bill_Mat.BillMat_Form.project_selected_str;
            //MySqlDataAdapter adapter = new MySqlDataAdapter( );
            //adapter.SelectCommand = cmd;
            //DataTable bill_mat_table = new DataTable( );
            //adapter.Fill( bill_mat_table );
            #endregion
            //CrystalReport2 var = new CrystalReport2( );

            //////var.Database.Tables[ "grand_total_DataTable" ].SetDataSource( main_scope_budget );
            //////var.Database.Tables[ "DataTable1" ].SetDataSource( sub_scope_budget );          

            //var.Database.Tables[ "bill_mat" ].SetDataSource( bill_mat_table );
            ////   var.SetParameterValue( "Project_name" , Bill_Mat.BillMat_Form.project_selected_str );
            //crystalReportViewer1.ReportSource = null;
            //crystalReportViewer1.ReportSource = var;
            //MySqlDataReader reader = cmd.ExecuteReader( );

            //while( reader.Read( ) )
            //{
            //    variance_report_DataTable.Rows.Add( string.Empty );
            //}
            //reader.Close( );
            /* ends here */
            //foreach( DataRow item in bill_mat_table.Rows )
            //{
            //    MessageBox.Show( item[3].ToString() );
            //}

        }

        private void Dashboard_form_FormClosed( object sender , FormClosedEventArgs e ) {
            Journal.Journal_form.print_journal = false;
            Bill_Mat.BillMat_Form.print_bill_mat = false;
        }
    }
}
