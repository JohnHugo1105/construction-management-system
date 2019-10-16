using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GSG_Builders.RevisionedUI.Database_connection;
//using MySql.Data.MySqlClient;
using GSG_Builders.RevisionedUI.Reusable_codes;
using System.Data.SqlClient;
using CmsLibrary.Model.SpEvents;
using CmsLibrary.Model.CostMonitoring;
using CmsLibrary;
using CmsLibrary.BusinessLogic.SpEvents;
using CmsLibrary.Model.CostMonitoring.ProjectSelection;

namespace GSG_Builders.RevisionedUI.Bill_Mat {
    public partial class ProjectConfigForm : Form {    
            
     
      
        string project_selected = "";
        string sub_project_selected = "";
        public static bool main_project_selected_clicked = false;
        public static bool sub_project_selected_clicked = false;
        public ProjectConfigForm() {
            InitializeComponent();
         

            BillMat_Form.new_project_form_count = 0;
        }

        public int Main_Project_Count() {

            int projcount = 0;

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.stored_proc_all , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.AddWithValue(Fields_reusable_base_class.event_parameter , Fields_reusable_base_class .select_event);
                    cmd.Parameters.AddWithValue( Fields_reusable_base_class.table_parameter , Fields_reusable_base_class .main_project_table);
                    cmd.Parameters.AddWithValue( Fields_reusable_base_class.col_select , "COUNT( *)" );
                    cmd.Parameters.AddWithValue( Fields_reusable_base_class.other_query , "WHERE project_name='" + project_name_txt.Text.Replace( "'" , "''" ) + "' " );

                    SqlDataReader reader = cmd.ExecuteReader( );

                    while( reader.Read( ) )
                    {
                        projcount = reader.GetInt32( 0 );
                    }
                    reader.Close( );
                    return projcount;
                }
            }
            #region old codes
            //cmd = new MySqlCommand();
            //cmd.Connection = MySQL_connection.GetConnection();
            //cmd.Connection.Open();
            //cmd.CommandText = "SELECT COUNT(*) FROM " + main_project_database_tbl + " WHERE project_name='" + project_name_txt.Text.Replace("'","''") + "'";
            //MySqlDataReader reader = cmd.ExecuteReader();

            //while (reader.Read())
            //{
            //    projcount = reader.GetInt32(0);
            //}
            //reader.Close();
            //return projcount;
            #endregion
        }

        private void CreateMainProject( ) {
            DateTime date = Convert.ToDateTime( date_started_dtp.Text );
            MainProjectsModel credential = new MainProjectsModel( project_name_txt.Text , date );
            GlobalConfig.MainProjectConfig.CreateMainProject( credential , SpProjectsEventsList.spMainProjectCreate , "MainProjects" );
        }

        private void CreateSubProject( ) {
            DateTime date = Convert.ToDateTime( date_started_dtp.Text );
            SubProjectsModel credential = new SubProjectsModel( sub_project_name_txt.Text , date );
            GlobalConfig.SubProjectConfig.CreateSubProject( credential , SpProjectsEventsList.spSubProjectCreate , "MainProjects" );
        }

        private int GetLatestIdentifier( string tableName) {            
            return GlobalConfig.ProjectsGlobalConfig.GetLatestIdentifier( tableName );
        }


        private void save_btn_Click(object sender, EventArgs e) {
           

           // if( project_name_txt.Text==string.Empty )
           // {
           //     MessageBox.Show( "Project name field empty!","Empty field",MessageBoxButtons.OK,MessageBoxIcon.Exclamation );
           // }          
           //else if(Main_Project_Count() == 0 )
           // {                
           //     if( sub_project_name_txt.Text == string.Empty )
           //     {


            //         MessageBox.Show( project_name_txt.Text.ToUpper( ) + " successfully created as main project! use add sub-project to add more sub-projects" , "Creating main project" , MessageBoxButtons.OK , MessageBoxIcon.Information );
            //     }
            //     else
            //     {

            //         MessageBox.Show( project_name_txt.Text.ToUpper( ) + " successfully added as main-project" , "Adding main-project" , MessageBoxButtons.OK , MessageBoxIcon.Information );


            //         //MessageBox.Show( project_name_txt.Text.ToUpper( ) + " (main project), " + sub_project_name_txt.Text.ToUpper( ) + " (sub-project)" + " successfully added" , "Adding main & sub-project" , MessageBoxButtons.OK , MessageBoxIcon.Information );
            //     }
            // }
            // else
            // {
            //     MessageBox.Show( "Project : " + project_name_txt.Text + " already exist!" , "Duplicate project" , MessageBoxButtons.OK , MessageBoxIcon.Exclamation );
            // }          
        }
        private void add_sub_project_btn_Click(object sender, EventArgs e) {
             
        }
        private bool form_close = false;
        private void back_toolStripButton_Click ( object sender , EventArgs e ) {
            form_close = true;
            Close( );
            Application.OpenForms[ "BillMat_Form" ].Activate( );
        }

        private void bill_mat_settings_main_project_dataGridView_CellContentDoubleClick ( object sender , DataGridViewCellEventArgs e ) {
            if ( Application.OpenForms.OfType<BillMat_Form>( ).Count( ) == 1 )
            {
                BillMat_Form.project_selected_str = main_project_dataGridView.CurrentRow.Cells[ "project_name" ].Value.ToString( );
                
                BillMat_Form.new_project_form_count = 1;
            }
            else if ( Application.OpenForms.OfType<Journal.Journal_form>( ).Count( ) == 1 )
            {             
                Journal.Journal_form.project_selected_str= main_project_dataGridView.CurrentRow.Cells[ "project_name" ].Value.ToString( );
                Journal.Journal_form.new_project_form_count = 1;
            }
           
            Close( );
        }

        private void bill_mat_settings_main_project_dataGridView_CellClick ( object sender , DataGridViewCellEventArgs e ) {
        
        }

        private void edit_toolStripButton_Click ( object sender , EventArgs e ) {
          
        }

        private void bill_mat_settings_sub_project_dataGridView_CellClick ( object sender , DataGridViewCellEventArgs e ) {
            sub_project_name_txt.Text = sub_project_dataGridView.CurrentRow.Cells[ "sub_project_name" ].Value.ToString( );
            sub_project_selected = sub_project_dataGridView.CurrentRow.Cells[ "sub_project_name" ].Value.ToString( );
            main_project_selected_clicked = false;
            sub_project_selected_clicked = true;
            
        }
       
        private void remove_toolStripButton_Click ( object sender , EventArgs e ) {
            string main_sub_project_value = "";
            string main_sub_selected = "";
     
            if ( project_selected==string.Empty )
            {
                MessageBox.Show( "Please select a project by clicking on the list of projects below!","No selection",MessageBoxButtons.RetryCancel,MessageBoxIcon.Information );
            }
            else
            {
                if( main_project_selected_clicked == true )
                {
                    main_sub_project_value = main_project_dataGridView.CurrentRow.Cells[ "project_name" ].Value.ToString( ).ToUpper( );
                    main_sub_selected = "project";
                  
                }
                else if(sub_project_selected_clicked == true )
                {
                    main_sub_project_value = sub_project_dataGridView.CurrentRow.Cells[ sub_project_dataGridView.CurrentCell.OwningColumn.Name ].Value.ToString( ).ToUpper( );
                    main_sub_selected = "sub-project";
                
                }
                DialogResult Yes = MessageBox.Show( "Are you sure you want to remove "+main_sub_selected +" : " + main_sub_project_value + "?" + Environment.NewLine + "all records of this project will be remove and move to archives", "Removing main or sub-project", MessageBoxButtons.YesNo, MessageBoxIcon.Question );

                if ( Yes == DialogResult.Yes )
                {
                    Archives.Archives_reason_form.main_project_selected = project_selected;
                    Archives.Archives_reason_form.sub_project_selected = sub_project_selected;
                    Archives.Archives_reason_form.date_started = main_project_dataGridView.CurrentRow.Cells[ "date_started" ].Value.ToString( );                    
                    Archives.Archives_reason_form reason = new Archives.Archives_reason_form( );
                    reason.Show( );
                }
            }           
        }     
     
        private void New_project_form_Load ( object sender , EventArgs e ) {
           
        }

        private void bunifuSeparator2_Load ( object sender , EventArgs e ) {

        }

        private void New_project_form_Activated ( object sender , EventArgs e ) {
       
        }

        private void main_project_dataGridView_CellMouseDown ( object sender , DataGridViewCellMouseEventArgs e ) {
            if( e.Button == MouseButtons.Right )
            {
                main_project_dataGridView.CurrentCell = main_project_dataGridView.Rows[ e.RowIndex ].Cells[ e.ColumnIndex ];
                main_project_dataGridView.Rows[ e.RowIndex ].Selected = true;
                main_project_selected_clicked = true;
                sub_project_selected_clicked = false;
                project_selected = main_project_dataGridView.CurrentRow.Cells[ "project_name" ].Value.ToString( );
            }
        }

        private void sub_project_dataGridView_CellMouseDown ( object sender , DataGridViewCellMouseEventArgs e ) {
            if( e.Button == MouseButtons.Right )
            {
                sub_project_dataGridView.CurrentCell = sub_project_dataGridView.Rows[ e.RowIndex ].Cells[ e.ColumnIndex ];
                sub_project_dataGridView.Rows[ e.RowIndex ].Selected = true;
                main_project_selected_clicked = false;
                sub_project_selected_clicked = true;
                sub_project_name_txt.Text = sub_project_dataGridView.CurrentRow.Cells[ "sub_project_name" ].Value.ToString( );
            }
        }

        private void deleteToolStripMenuItem_Click ( object sender , EventArgs e ) {
           Remove_record ar = new Remove_record( );
            Methods_return_type get = new Methods_return_type( );
            string main_sub_project_value = "";
            string main_sub_selected = "";
       //     string message = "";
       
            if( main_project_selected_clicked == true )
            {
                main_sub_project_value = main_project_dataGridView.CurrentRow.Cells[ "project_name" ].Value.ToString( ).ToUpper( );
                main_sub_selected = "project";                
            }
            else if( sub_project_selected_clicked == true )
            {
                main_sub_project_value = sub_project_dataGridView.CurrentRow.Cells[ sub_project_dataGridView.CurrentCell.OwningColumn.Name ].Value.ToString( ).ToUpper( );
                main_sub_selected = "sub-project";                
            }

            DialogResult Yes = MessageBox.Show( "Are you sure you want to permanently delete " + main_sub_selected + " : " + main_sub_project_value + "?" + Environment.NewLine + "all records of this project will be permanently deleted" , "Deletion" , MessageBoxButtons.YesNo , MessageBoxIcon.Warning );

            if( main_project_selected_clicked == true )
            {
          
            }
            else if( sub_project_selected_clicked == true )
            {
                if( project_selected==string.Empty )
                {
                    MessageBox.Show( "No selected project on MAIN PROJECT list, select by clicking the table list","No selection",MessageBoxButtons.RetryCancel,MessageBoxIcon.Asterisk );
                }
                else
                {
               
                }                
            }
         
        }

        private void label1_Click( object sender , EventArgs e ) {
            //Methods_return_type get = new Methods_return_type( );
            //get.Main_Project_Id_save_sub_project( project_name_txt.Text.Replace( "'" , "''" ) );
        }

        private void New_project_form_Deactivate( object sender , EventArgs e ) {
            if( form_close!=true )
            {
                Form_opacity_fade.form_opacity_fade( this);
            }            
        }

        private void addBothToolStripMenuItem_Click( object sender , EventArgs e ) {
            Console.WriteLine( GetLatestIdentifier("MainProjects" ).ToString());
        }

        private void CreateMainProjectBtn_Click( object sender , EventArgs e ) {
            CreateMainProject( );
        }

        private void CreateSubProjectBtn_Click( object sender , EventArgs e ) {
            CreateSubProject( );
        }
    }
}
