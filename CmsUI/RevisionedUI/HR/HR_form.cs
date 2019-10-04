using GSG_Builders.RevisionedUI.Database_connection;
using GSG_Builders.RevisionedUI.Reusable_codes;
//using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.HR {
    public partial class HR_form : Form {

        public static string GetPicPath;

        public static string worker_biodata_id;
        public static string first_name;
        public static string middle_name;
        public static string surname;
        public static string id_number;
        public static string designation;
        public static string project_assigned;
        public static string rate;

        public static float percentage_size = 0;

        int start = 0;
        int pageCount = 1;
        int current_row = 0;
        //MySqlDataAdapter adapter = new MySqlDataAdapter( );
        DataSet ds = new DataSet( );
        DataTable table;
        public HR_form( ) {
            InitializeComponent( );
            tabControl1.ItemSize = new Size( 0 , 1 );
            tabControl1.SizeMode = TabSizeMode.Fixed;
            Autocomplete_source source = new Autocomplete_source( );
            source.Autocomplete_main_project( project_assigned_textBox );
            checkbox_column( );
            load_record( );
        }
        private void checkbox_column( ) {
            datagridview_configuration checkbox = new datagridview_configuration( hr_biodata_datagridView );
            checkbox.checkboxColumn( );
        }
        private void load_record( ) {
       

        }

        private void worker_image_show( ) {
            var worker_image = hr_biodata_datagridView.CurrentRow.Cells[ "worker_picture" ].Value;
            byte[ ] image = ( byte[ ] ) ( worker_image );
            MemoryStream MStream = new MemoryStream( image );
            worker_image_bunifuImageButton.Image = Image.FromStream( MStream );
        }

        public void next_record( ) {
            if( current_row <= hr_biodata_datagridView.RowCount - 2 )
            {
                hr_biodata_datagridView.Rows[ current_row ].Selected = false;
                hr_biodata_datagridView.Rows[ ++current_row ].Selected = true;
                hr_biodata_datagridView.CurrentCell = hr_biodata_datagridView.Rows[ current_row ].Cells[ hr_biodata_datagridView.CurrentCell.ColumnIndex ];

                worker_image_show( );
            }
        }

        private void prev_record( ) {
            if( current_row > 0 )
            {
                hr_biodata_datagridView.Rows[ current_row ].Selected = false;
                hr_biodata_datagridView.Rows[ --current_row ].Selected = true;
                hr_biodata_datagridView.CurrentCell = hr_biodata_datagridView.Rows[ current_row ].Cells[ hr_biodata_datagridView.CurrentCell.ColumnIndex ];

                worker_image_show( );
            }
        }

        private void click_show_worker_records( ) {
      

            datagridview_configuration config = new datagridview_configuration( case_dataGridView );
            config.case_dataGridView( );

            datagridview_configuration config2 = new datagridview_configuration( designation_rate_dataGridView );
            config2.designation_rate_dataGridView( );

            datagridview_configuration config3 = new datagridview_configuration( assigned_projects_dataGridView );
            config3.assigned_projects_dataGridView( );
        }

        private void static_fields( ) {
            worker_biodata_id = hr_biodata_datagridView.CurrentRow.Cells[ "worker_biodata_id" ].Value.ToString( );
            first_name = hr_biodata_datagridView.CurrentRow.Cells[ "first_name" ].Value.ToString( );
            middle_name = hr_biodata_datagridView.CurrentRow.Cells[ "middle_name" ].Value.ToString( );
            surname = hr_biodata_datagridView.CurrentRow.Cells[ "surname" ].Value.ToString( );
            id_number = hr_biodata_datagridView.CurrentRow.Cells[ "id_number" ].Value.ToString( );
            designation = hr_biodata_datagridView.CurrentRow.Cells[ "designation" ].Value.ToString( );
            project_assigned = hr_biodata_datagridView.CurrentRow.Cells[ "project_assigned" ].Value.ToString( );
            rate = hr_biodata_datagridView.CurrentRow.Cells[ "rate" ].Value.ToString( );
        }

        private void clear_textbox( ) {
            first_name_textBox.Clear( );
            middle_name_textBox.Clear( );
            surname_textBox.Clear( );
            designation_textBox.Clear( );
            rate_textBox.Clear( );
            id_number_textBox.Clear( );
            status_comboBox.Text = "";
            wife_name_textBox.Clear( );
            religion_textBox.Clear( );
            educational_attaintment_textBox.Clear( );
            provintial_address_textBox.Clear( );
            present_address_textBox.Clear( );
            reference_textBox.Clear( ); contact_number_textBox.Clear( );
            provintial_address_textBox.Clear( );
            project_assigned_textBox.Clear( );
            worker_picture_bunifuImageButton.Image = Properties.Resources.iconfinder_image_add_103590_128_x_128; ;
        }

        private void search_records( ) {           
            table = new DataTable( );
      

            if( search_metroTextBox.Text == "Search" )
            {
                load_record( );
                next_page_toolStripButton.Enabled = true;
                prev_page_toolStripButton.Enabled = true;
            }
            else
            {
                next_page_toolStripButton.Enabled = false;
                prev_page_toolStripButton.Enabled = false;

                DataView view = new DataView( table );
                view.RowFilter = string.Format( "first_name LIKE '%{0}%' or middle_name LIKE '%{0}%' or surname LIKE '%{0}%' or project_assigned LIKE '%{0}%' or designation LIKE '%{0}%' or id_number LIKE '%{0}%' " , search_metroTextBox.Text.Replace( "'" , "''" ) );
                hr_biodata_datagridView.DataSource = view;
            }
        }

        private void search_records_with_filter( ) {
            table = new DataTable( );
         

            if( search_metroTextBox.Text == "Search" )
            {
                load_record( );
                next_page_toolStripButton.Enabled = true;
                prev_page_toolStripButton.Enabled = true;
            }
            else
            {
                next_page_toolStripButton.Enabled = false;
                prev_page_toolStripButton.Enabled = false;

                DataView view = new DataView( table );
                view.RowFilter = string.Format( "first_name LIKE '%{0}%' or middle_name LIKE '%{0}%' or surname LIKE '%{0}%' or project_assigned LIKE '%{0}%' or designation LIKE '%{0}%' or id_number LIKE '%{0}%' and worker_status LIKE '%{1}%' " , search_metroTextBox.Text.Replace( "'" , "''" ),filter_comboBox.Text.Replace( "'" , "''" ) );
                hr_biodata_datagridView.DataSource = view;
            }
        }

        private void form_opacity_fade( ) {
            this.Opacity = 0.8;
        }

        private void minimize_pictureBox_Click( object sender , EventArgs e ) {
            WindowState = FormWindowState.Minimized;
        }

        private void close_pictureBox_Click( object sender , EventArgs e ) {
            Environment.Exit( Environment.ExitCode );
        }

        private void worker_information_bunifuTileButton1_Click( object sender , EventArgs e ) {
            worker_information_bunifuTileButton1.color = Color.SteelBlue;
            new_wroker_entry_bunifuTileButton.color = Color.PowderBlue;
            print_report_bunifuTileButton.color = Color.PowderBlue;
            tabControl1.SelectedTab = workers_information_tabPage;
        }

        private void individual_ToolStripMenuItem_Click( object sender , EventArgs e ) {
            static_fields( );
            Manpower.Workers_information.Individual_assignment_form indi = new Manpower.Workers_information.Individual_assignment_form( );
            indi.ShowDialog( );
        }

        private void new_wroker_entry_bunifuTileButton_Click( object sender , EventArgs e ) {
            new_wroker_entry_bunifuTileButton.color = Color.SteelBlue;
            worker_information_bunifuTileButton1.color = Color.PowderBlue;
            print_report_bunifuTileButton.color = Color.PowderBlue;
            tabControl1.SelectedTab = new_worker_entry_tabPage;
        }

        private void print_report_bunifuTileButton_Click( object sender , EventArgs e ) {
            worker_information_bunifuTileButton1.color = Color.PowderBlue;
            new_wroker_entry_bunifuTileButton.color = Color.PowderBlue;
            print_report_bunifuTileButton.color = Color.SteelBlue;
            tabControl1.SelectedTab = print_report_tabPage;

        }

        private void new_worker_entry_tabPage_Click( object sender , EventArgs e ) {

        }

        private void save_entry_button_MouseHover( object sender , EventArgs e ) {

        }

        private void panel22_Click( object sender , EventArgs e ) {

        }

        private void bunifuImageButton2_MouseHover( object sender , EventArgs e ) {

        }

        private void HR_form_Load( object sender , EventArgs e ) {

        }

        private void bunifuImageButton2_MouseLeave( object sender , EventArgs e ) {

        }

        private void save_entry_button_MouseLeave( object sender , EventArgs e ) {

        }

        private void multiple_ToolStripMenuItem_Click( object sender , EventArgs e ) {
            Manpower.Workers_information.multiple_assignment assign = new Manpower.Workers_information.multiple_assignment( );
            assign.ShowDialog( );
        }

        private void bunifuImageButton1_MouseHover( object sender , EventArgs e ) {
            //  worker_picture_bunifuImageButton.SizeMode = PictureBoxSizeMode.Zoom;
        }

        private void bunifuImageButton1_MouseLeave( object sender , EventArgs e ) {
            // worker_picture_bunifuImageButton.SizeMode = PictureBoxSizeMode.CenterImage;
        }

        private void changeDesignationToolStripMenuItem_Click( object sender , EventArgs e ) {
            static_fields( );

            Manpower.Workers_information.Change_designation_rate_form change = new Manpower.Workers_information.Change_designation_rate_form( );
            change.ShowDialog( );
        }

        private void caseToolStripMenuItem_Click( object sender , EventArgs e ) {
            static_fields( );

            Manpower.Workers_information.Workers_case_form Case = new Manpower.Workers_information.Workers_case_form( );
            Case.ShowDialog( );
        }
        HR_form hr;
        private void home_bunifuTileButton_Click( object sender , EventArgs e ) {
            try
            {
                if( Application.OpenForms.OfType<HR_form>( ).Count( ) == 1 )
                {
                    timer_Fadeout.Start( );
                }
                else
                {
                    hr = new HR_form( );
                    hr.FormClosed += Hr_FormClosed;
                    hr.Show( );
                }
            }
            catch( Exception ex )
            {

                MessageBox.Show( "Error! : " + ex.Message , "ERROR!" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Error );
            }
        }

        private void Hr_FormClosed( object sender , FormClosedEventArgs e ) {
            hr = null;
        }

        private void timer_Fadeout_Tick( object sender , EventArgs e ) {
            if( this.Opacity > 0.0 )
            {
                this.Opacity -= 0.075;
            }
            else
            {
                timer_Fadeout.Stop( );
                this.Close( );
                Application.OpenForms[ "Home_Form" ].Show( );
                Application.OpenForms[ "Home_Form" ].WindowState = FormWindowState.Normal;
                Application.OpenForms[ "Home_Form" ].BringToFront( );
            }
        }

        private void save_bunifuThinButton_Click( object sender , EventArgs e ) {
         
        }

        private void worker_picture_bunifuImageButton_Click( object sender , EventArgs e ) {
            try
            {
                GetPicPath = SelectImage.ImagePath;
                SelectImage select = new SelectImage( );
                select.SelectAnImage( worker_picture_bunifuImageButton , GetPicPath );
            }
            catch( Exception ex )
            {
                MessageBox.Show( ex.Message );
            }
        }

        private void workers_information_tabPage_Click( object sender , EventArgs e ) {

        }

        private void clear_bunifuThinButton_Click( object sender , EventArgs e ) {

        }

        private void hr_biodata_datagridView_CellClick( object sender , DataGridViewCellEventArgs e ) {
            click_show_worker_records( );
            edit_mode = false;
            if( e.RowIndex >= 0 && e.ColumnIndex >= 0 )
            {
                hr_biodata_datagridView.CurrentCell = hr_biodata_datagridView.Rows[ e.RowIndex ].Cells[ e.ColumnIndex ];
                hr_biodata_datagridView.Rows[ e.RowIndex ].Selected = true;
                current_row = hr_biodata_datagridView.CurrentRow.Index;
            }

            if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name == "checkbox" && edit_mode==false )
            {
                edit_mode_off( );
            }
            worker_image_show( );
        }

        private void hr_biodata_datagridView_CellValueChanged( object sender , DataGridViewCellEventArgs e ) {
            foreach( DataGridViewRow item in hr_biodata_datagridView.Rows )
            {
                bool check = Convert.ToBoolean( item.Cells[ 0 ].Value );
                if( check == true )
                {
                    item.Selected = true;
                }
            }
        }

        private void hr_biodata_datagridView_CurrentCellDirtyStateChanged( object sender , EventArgs e ) {
            if( hr_biodata_datagridView.IsCurrentCellDirty )
            {
                hr_biodata_datagridView.CommitEdit( DataGridViewDataErrorContexts.Commit );
            }
        }

        private void hr_biodata_datagridView_CellMouseDown( object sender , DataGridViewCellMouseEventArgs e ) {
            if( e.Button == MouseButtons.Right )
            {
                hr_biodata_datagridView.CurrentCell = hr_biodata_datagridView.Rows[ e.RowIndex ].Cells[ e.ColumnIndex ];
                hr_biodata_datagridView.Rows[ e.RowIndex ].Selected = true;
                click_show_worker_records( );
                hr_biodata_datagridView.Focus( );
                worker_image_show( );
            }
        }
        private void edit_mode_off( ) {
      
            hr_biodata_datagridView.ReadOnly = false;
            hr_biodata_datagridView.Columns[ "first_name" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "middle_name" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "surname" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "project_assigned" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "worker_status" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "designation" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "rate" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "id_number" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "date_hired" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "date_of_birth" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "status" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "wife_name" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "religion" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "educational_attainment" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "present_address" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "provintial_address" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "reference" ].ReadOnly = true;
            hr_biodata_datagridView.Columns[ "contact_number" ].ReadOnly = true;
        }
        private void edit_mode_on( ) {
            hr_biodata_datagridView.ReadOnly = false;
            hr_biodata_datagridView.Columns[ "first_name" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "middle_name" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "surname" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "project_assigned" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "designation" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "rate" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "id_number" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "date_hired" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "date_of_birth" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "status" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "wife_name" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "religion" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "educational_attainment" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "present_address" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "provintial_address" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "reference" ].ReadOnly = false;
            hr_biodata_datagridView.Columns[ "contact_number" ].ReadOnly = false;
        }
        bool edit_mode = false;
        private void editToolStripMenuItem_Click( object sender , EventArgs e ) {
            edit_mode = true;
            if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "checkbox" )
            {
                if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name == "date_hired" && hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "worker_status" || hr_biodata_datagridView.CurrentCell.OwningColumn.Name == "date_of_birth" && hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "worker_status" )
                {
                    date_hired_birthdate_Edit_dateTimePicker1.Location = new Point( hr_biodata_datagridView.GetCellDisplayRectangle( hr_biodata_datagridView.CurrentCell.ColumnIndex , row_index , false ).Location.X + hr_biodata_datagridView.CurrentCell.Size.Width - 20 , cell_y_position( ) + 50 );
                    date_hired_birthdate_Edit_dateTimePicker1.Text = hr_biodata_datagridView.CurrentRow.Cells[ hr_biodata_datagridView.CurrentCell.OwningColumn.Name ].Value.ToString( );
                    date_hired_birthdate_Edit_dateTimePicker1.Visible = true;
                }
                if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name == "worker_status" )
                {
                    contextMenuStrip2.Show( hr_biodata_datagridView , hr_biodata_datagridView.GetCellDisplayRectangle( hr_biodata_datagridView.CurrentCell.ColumnIndex , row_index , false ).Location.X + hr_biodata_datagridView.CurrentCell.Size.Width - 20 , cell_y_position( )  );
                }
                else
                {
                    edit_mode_on( );
                    hr_biodata_datagridView.BeginEdit( true );
                }
            }           
        }
        private int cell_y_position( ) {
            int y = 0;
            return y = ( ( hr_biodata_datagridView.CurrentRow.Index + 3 ) * hr_biodata_datagridView.RowTemplate.Height - 5 );
        }

        int row_index = 0;
        private void hr_biodata_datagridView_RowLeave( object sender , DataGridViewCellEventArgs e ) {
            row_index = e.RowIndex;
        }

        private void date_hired_Edit_dateTimePicker1_CloseUp( object sender , EventArgs e ) {
            date_hired_birthdate_Edit_dateTimePicker1.Visible = false;

            Update_records update = new Update_records( );
            Update_records update_date_for_sorting = new Update_records( );
            string date_value = "";

            if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name == "date_hired"  || hr_biodata_datagridView.CurrentCell.OwningColumn.Name == "date_of_birth" )
            {
                date_value = date_hired_birthdate_Edit_dateTimePicker1.Text;


                if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name == "date_hired" )
                {
                   
                }                

             
            }
           
        }

        private void hr_biodata_datagridView_CellEndEdit( object sender , DataGridViewCellEventArgs e ) {
            Update_records update = new Update_records( );
          //  hr_biodata_datagridView.CancelEdit( );
            if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name!="checkbox" )
            {
                if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "date_hired" && hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "project_assigned" && hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "worker_status" || hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "date_of_birth" && hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "project_assigned" && hr_biodata_datagridView.CurrentCell.OwningColumn.Name != "worker_status" )
                {
                  
                }              
            }
            if( hr_biodata_datagridView.CurrentCell.OwningColumn.Name  == "project_assigned" )
            {
                MessageBox.Show( "Cannot edit project name, to edit project name follow the ff. steps : " + Environment.NewLine + "Home>Project>settings>select project on list>type project name>click edit button" , "Edit project name not allowed" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Warning );
                edit_mode_off( );
            }
        }
        bool click_max = false;
        private void bunifuImageButton2_Click( object sender , EventArgs e ) {
            if( click_max==false )
            {
                history_groupBox.Height = history_groupBox.Height - 110;
                history_groupBox.Location = new Point( history_groupBox.Location.X , history_groupBox.Location.Y + 110 );
                biodata_groupBox.Height = biodata_groupBox.Height + 110;
                click_max = true;
            }
            else
            {
                history_groupBox.Height = history_groupBox.Height + 110;
                history_groupBox.Location = new Point( history_groupBox.Location.X , history_groupBox.Location.Y - 110 );
                biodata_groupBox.Height = biodata_groupBox.Height - 110;
                click_max = false;
            }
          
        }

        private void hr_biodata_datagridView_CellMouseEnter( object sender , DataGridViewCellEventArgs e ) {
            
        }

        private void hr_biodata_datagridView_MouseHover( object sender , EventArgs e ) {
           
        }

        private void hr_biodata_datagridView_MouseMove( object sender , MouseEventArgs e ) {
           
        }

        private void next_record_toolStripButton_Click( object sender , EventArgs e ) {
            next_record( );
            click_show_worker_records( );
        }

        private void prev_record_toolStripButton_Click( object sender , EventArgs e ) {
            prev_record( );
            click_show_worker_records( );
        }

        private void search_metroTextBox_Click( object sender , EventArgs e ) {
            if( search_metroTextBox.Text == "Search" )
            {
                search_metroTextBox.Clear( );
                search_metroTextBox.ForeColor = Color.Black;
            }
        }

        private void search_metroTextBox_KeyDown( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Enter && search_metroTextBox.Text != string.Empty && search_metroTextBox.Text != "Search" && filter_comboBox.Text==string.Empty )
            {
                search_records( );
            }
            else if( e.KeyCode == Keys.Enter && search_metroTextBox.Text != string.Empty && search_metroTextBox.Text != "Search" && filter_comboBox.Text != string.Empty )
            {
                search_records_with_filter( );
            }
            else if( e.KeyCode == Keys.Enter && search_metroTextBox.Text == string.Empty )
            {
                load_record( );
                filter_comboBox.Text = "";
            }
        }

        private void search_metroTextBox_Leave( object sender , EventArgs e ) {
            if( search_metroTextBox.Text == string.Empty )
            {
                search_metroTextBox.ForeColor = Color.DarkGray;
                search_metroTextBox.Text = "Search";
                filter_comboBox.Text = "";
                load_record( );
            }
        }

        private void refresh_bunifuImageButton_Click( object sender , EventArgs e ) {
            load_record( );
            edit_mode_off( );
            worker_image_bunifuImageButton.Image = Properties.Resources.iconfinder_image_add_103590_128_x_128;
            search_metroTextBox.Clear( );
            filter_comboBox.Text = "";
        }
        private float maximize_datagridviews(int column_index ) {          
            int count = 0;
            TableLayoutColumnStyleCollection styles = history_records_tableLayoutPanel.ColumnStyles;
            foreach( ColumnStyle item in styles )
            {                
                if( count<column_index )
                {                    
                    item.SizeType = SizeType.Percent;
                    percentage_size = item.Width;
                }
                count++;   
            }
            return percentage_size;
        }

        private void maximize_datagridviews_set_new_size( int column_index ) {
            int count = 1;
            TableLayoutColumnStyleCollection styles = history_records_tableLayoutPanel.ColumnStyles;
            foreach( ColumnStyle item in styles )
            {
                if( count == column_index )
                {
                    item.SizeType = SizeType.Percent;
                    item.Width = percentage_size ;
                }
                count++;
            }           
        }

        bool case_selected = false;
        bool designation_selected = false;
        bool assigned_selected = false;
        int tablelayout_column_index = 0;
        private void maximize_case_bunifuImageButton_Click( object sender , EventArgs e ) {      
            maximize_datagridviews( 3 );
            case_selected = true;
            tablelayout_column_index = 3;
            maximizing_form max = new maximizing_form( );
            max.ShowDialog( );
            
        }

        private void maximize_change_designation_bunifuImageButton_Click( object sender , EventArgs e ) {
           maximize_datagridviews( 2 );
            designation_selected = true;
            tablelayout_column_index = 2;
            maximizing_form max = new maximizing_form( );
            max.ShowDialog( );
        }

        private void maximize_assigned_projects_bunifuImageButton_Click( object sender , EventArgs e ) {
             maximize_datagridviews( 1 );
            assigned_selected = true;
            tablelayout_column_index = 1;
            maximizing_form max = new maximizing_form( );
            max.ShowDialog( );
        }

        private void HR_form_Activated( object sender , EventArgs e ) {
            if( maximizing_form.size_set == true )
            {
                maximize_datagridviews_set_new_size( tablelayout_column_index );
              
                    maximize_assigned_projects_bunifuImageButton.Location = new Point( history_groupBox.Location.X + assigned_projects_label.Width - maximize_assigned_projects_bunifuImageButton.Width - 2 , maximize_assigned_projects_bunifuImageButton.Location.Y );
            
                    maximize_change_designation_bunifuImageButton.Location = new Point( history_groupBox.Location.X + (assigned_projects_label.Width + change_designation_label.Width) - maximize_assigned_projects_bunifuImageButton.Width + 2 , maximize_change_designation_bunifuImageButton.Location.Y );
            
                    maximize_case_bunifuImageButton.Location = new Point( history_groupBox.Location.X + ( assigned_projects_label.Width + change_designation_label.Width + case_records_label.Width ) - maximize_assigned_projects_bunifuImageButton.Width + 5 , maximize_case_bunifuImageButton.Location.Y );
             
                maximizing_form.size_set = false;
            }
            Opacity = 1.0;
        }

        private void removeToolStripMenuItem_Click( object sender , EventArgs e ) {

        }

        private void worker_status_toolStripComboBox_DropDownClosed( object sender , EventArgs e ) {
          
        }

        private void worker_status_toolStripComboBox_SelectedIndexChanged( object sender , EventArgs e ) {
           
        }

        private void contextMenuStrip2_Closed( object sender , ToolStripDropDownClosedEventArgs e ) {
            
        }

        private void worker_status_toolStripComboBox_KeyDown( object sender , KeyEventArgs e ) {
            Update_records update = new Update_records( );

         
            edit_mode_off( );
            contextMenuStrip2.Hide( );
        }

        private void filter_comboBox_SelectedIndexChanged( object sender , EventArgs e ) {
            table = new DataTable( );
         

            DataView view = new DataView( table );
            view.RowFilter = string.Format( "worker_status LIKE '%{0}%'  " , filter_comboBox.Text.Replace( "'" , "''" ) );
            hr_biodata_datagridView.DataSource = view;
        }

        private void next_page_toolStripButton_Click( object sender , EventArgs e ) {
          
        }
     
        private void HR_form_Leave( object sender , EventArgs e ) {
            
        }

        private void HR_form_Deactivate( object sender , EventArgs e ) {
            Form_opacity_fade.form_opacity_fade( this );
        }
    }
}
