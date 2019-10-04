using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GSG_Builders.RevisionedUI.Home;
//using MySql.Data.MySqlClient;
using GSG_Builders.RevisionedUI.Bill_Mat;
using GSG_Builders.RevisionedUI.Reusable_codes;
using System.Data.SqlClient;
using GSG_Builders.RevisionedUI.Database_connection;

namespace GSG_Builders.RevisionedUI.Journal {
    public partial class Journal_form : Form  {

        SqlDataAdapter adapter = new SqlDataAdapter( );
        DataSet ds = new DataSet( );

        Autocomplete_source complete = new Autocomplete_source( );

        Control active_control;//this is to use for the focus and leave events of the controls for record entry inputs
                               /// <summary>
                               /// /Sets the hint texts of the active controls
                               /// </summary>

        List<string> DivcmbItems = new List<string>( );//this is to get the default items in the comboBox of Division no.

        public static string project_selected_str="";//wil hold the value name of the selected project

        int current_row = 0;

        public static int new_project_form_count = 0;

        public static bool print_journal = false;

        public Journal_form() {
            InitializeComponent();

            /* sets all the autocomplete source of controls */        
            complete.Autocomplete_project( main_project_comboBox );
            complete.Autocomplete_sub_project( main_project_comboBox , sub_project_comboBox );
            complete.Autocomplete_main_scope_description( main_scope_description_comboBox );
            complete.Autocomplete_sub_scope_description( sub_scope_description_comboBox );
            complete.Autocomplete_item_description( item_description_textBox );
            complete.Autocomplete_unit_measurement( unit_comboBox );
            /* ends here */

            checkbox_column( );

            load_record( );

            total_expense( );

            foreach( string item in division_num_comboBox.Items )
            {
                DivcmbItems.Add( item );//adding the default items in Division no. combobox                                                    
            }
        }

        private static string main_project_str = "main_project_comboBox";
        private static string sub_project_str = "sub_project_comboBox";
        private static string DivisionNum_comboBox_str = "DivisionNum_comboBox";
        private static string wbs_code_str = "wbs_Code_comboBox";
        private static string main_scope_description_comboBox_str = "main_scope_description_comboBox";
        private static string material_item_comboBox_str = "material_labor_comboBox";
        private static string item_description_textBox_str = "item_description_textBox";
        private static string source_dealer_textBox_str = "source_dealer_textBox";
        private static string sub_scope_description_comboBox_str = "sub_scope_description_comboBox";
        private static string invoice_num_textBox_str = "invoice_num_textBox";
        private static string remarks_textBox_str = "remarks_textBox";
        private static string quantity_txt_str = "quantity_textBox";
        private static string unit_txt_str = "unit_comboBox";

        private void Division_number_inputting_value ( ) {
            int count = 0;
            selection_automation bill = new selection_automation( );
            bill.sub_scope_selection( sub_scope_description_comboBox , division_num_comboBox , main_scope_description_comboBox );

            Autocomplete_source selection = new Autocomplete_source( );

            string[ ][ ] indexArr = new string[ ][ ] { new string[ selection.Autocomplete_sub_scope_selection_get_items( sub_scope_description_comboBox , division_num_comboBox , main_scope_description_comboBox ).Length ] };

            foreach( var item2 in selection.Autocomplete_sub_scope_selection_get_items( sub_scope_description_comboBox , division_num_comboBox , main_scope_description_comboBox ) )
            {
                indexArr[ 0 ][ count ] = item2;
                sub_scope_description_comboBox.AutoCompleteCustomSource.Add( item2 );
                count++;
            }
            if( sub_scope_description_comboBox.Text != "-Select sub-scope description-" )
            {
                sub_scope_description_comboBox.ForeColor = Color.Black;
            }
            main_scope_description_comboBox.Enabled = false;
        }

        double start = 0;
        int pageCount = 1;

        private void load_record ( ) {
       
        }

        private void checkbox_column ( ) {
            datagridview_configuration checkbox = new datagridview_configuration( journal_datagridView );
            checkbox.checkboxColumn( );
        }

        private string expense_revenue_value ( ) {
            string value = "";
            //if( expense_bunifuCheckbox.Checked )
            //{
            //    value = "expense";
            //}
            //if( revenue_bunifuCheckbox.Checked )
            //{
            //    value = "revenue";
            //}
            return value;
        }
        private void total_expense ( ) {
            Computation com = new Computation( );

            journal_total_expense_metroTextBox.Text = com.compute_total_amount( project_selected_str , Fields_reusable_base_class.journal_table );
            total_vat_exclusive_metroTextBox.Text = com.compute_vat_exclusive( project_selected_str , Fields_reusable_base_class.journal_table );
            total_vat_metroTextBox.Text = com.compute_vat( project_selected_str , Fields_reusable_base_class.journal_table );
        }
        private void refresh_control ( ) {
            save_refresh_controls clear = new save_refresh_controls( );
            clear.Clear_text_journal( main_project_comboBox , sub_project_comboBox , division_num_comboBox , wbs_Code_comboBox , main_scope_description_comboBox , material_labor_comboBox , sub_scope_description_comboBox , item_description_textBox , remarks_textBox , quantity_textBox , unit_comboBox , unit_cost_metroTextBox , total_amount_metroTextBox , datepurchased_Label,source_dealer_textBox,invoice_sales_number_textBox ,  vat_exclusive_metroTextBox ,  vat_metroTextBox );

            sub_scope_description_comboBox.AutoCompleteCustomSource.Clear( );
            complete.Autocomplete_sub_scope_description( sub_scope_description_comboBox );

            sub_scope_description_comboBox.Text = "-Select sub-scope description-";
        }
        private void Data_fields_unfocus_setup ( ) {

            if( active_control.GetType( ).ToString( ).Remove( 0 , 21 ) == "ComboBox" || active_control.GetType( ).ToString( ).Remove( 0 , 21 ) == "TextBox" || active_control.GetType( ).ToString( ).Contains( "MetroTextBox" ) )
            {
                if( active_control.Text == "" )
                {
                    if( active_control.Name == main_project_str )
                    {
                        active_control.Text = "-Select main project-";
                    }
                    else if( active_control.Name == sub_project_str )
                    {
                        active_control.Text = "-Select sub-project-";
                    }
                    else if( active_control.Name == DivisionNum_comboBox_str )
                    {
                        active_control.Text = "-Select division no.-";
                    }
                    else if( active_control.Name == wbs_code_str )
                    {
                        active_control.Text = "-Select WBS code-";
                    }
                    else if( active_control.Name == main_scope_description_comboBox_str )
                    {
                        active_control.Text = "-Select main scope description-";
                    }
                    else if( active_control.Name == material_item_comboBox_str )
                    {
                        active_control.Text = "-Select material/labor-";
                    }
                    else if( active_control.Name == source_dealer_textBox_str )
                    {
                        active_control.Text = "Source/Dealer";
                    }
                    else if( active_control.Name == invoice_num_textBox_str )
                    {
                        active_control.Text = "Invoice/Sales no.";
                    }
                    else if( active_control.Name == sub_scope_description_comboBox_str )
                    {
                        active_control.Text = "-Select sub-scope description-";
                    }
                    else if( active_control.Name == item_description_textBox_str )
                    {
                        active_control.Text = "Item description";
                    }
                    else if( active_control.Name == remarks_textBox_str )
                    {
                        active_control.Text = "Remarks";
                    }
                    else if( active_control.Name == quantity_txt_str )
                    {
                        active_control.Text = "Quantity";
                    }
                    else if( active_control.Name == unit_txt_str )
                    {
                        active_control.Text = "-Select unit measurement-";
                    }
                    else if( active_control.GetType( ).ToString( ).Contains( "MetroTextBox" ) )
                    {
                        unit_cost_metroTextBox.Text = "Unit cost";
                        unit_cost_metroTextBox.ForeColor = Color.DarkGray;
                    }
                    active_control.ForeColor = Color.DarkGray;
                }
                active_control = this.ActiveControl;
            }
        }

        string text = "";
        private void Data_fields_focus_setup (  ) {
            active_control = this.ActiveControl;
            if( active_control.GetType( ).ToString( ).Remove( 0 , 21 ) == "ComboBox" || active_control.GetType( ).ToString( ).Remove( 0 , 21 ) == "TextBox" || active_control.GetType( ).ToString( ).Contains( "MetroTextBox" ) )
            {
                text = active_control.Text;
                if( active_control.ForeColor != Color.Black )
                {
                    active_control.Text = "";
                }
                if( active_control.GetType( ).ToString( ).Contains( "MetroTextBox" ) )
                {
                    unit_cost_metroTextBox.ForeColor = Color.Black;
                }                
                else
                {
                    active_control.ForeColor = Color.Black;
                }              
                active_control = this.ActiveControl;
            }        
        }

        private void search_records ( ) {
            if ( search_metroTextBox.Text == "Search" )
            {
                load_record( );
                next_page_toolStripButton.Enabled = true;
                prev_page_toolStripButton.Enabled = true;
            }
            else
            {
             
            }
        }

        private void timer1_fade_Tick(object sender, EventArgs e) {          
                if (this.Opacity > 0.0)
                {
                    this.Opacity -= 0.075;
                }
                else
                {
                    timer_Fadeout.Stop();
                    this.Close();
                    Application.OpenForms[ "Home_Form" ].Show();
                    Application.OpenForms[ "Home_Form" ].WindowState = FormWindowState.Normal;
                    Application.OpenForms[ "Home_Form" ].BringToFront();
                }
        }
     
        private void Journal_form_Load(object sender, EventArgs e) {
         
        }
        Home_Form Home_form;
        private void toolstripBtn_back_Click(object sender, EventArgs e) {
          
        }

        private void Home_form_FormClosed(object sender, FormClosedEventArgs e) {
            Home_form = null;
        }
      


        private void bindingNavigatorMoveNextItem_Click(object sender, EventArgs e) {
          
        }

        private void save_toolStripButton_Click(object sender, EventArgs e) {
            string[ ] controls_text_array = new string[ ] { "-Select main project-" , "-Select sub-project-" , "-Select division no.-" , "-Select main scope description-" , "-Select sub-scope description-" , "-Select WBS code-" , "Item description" , "-Select material/labor-" , "Source/Dealer", "Invoice/Sales no." , "Quantity" , "-Select unit measurement-" , "Remarks" , "Unit cost" , "Total amount", "Source/Dealer", "Invoice/Sales no." };
           
            string[ ] controls_array = new string[ ] { "main_project_comboBox" , "sub_project_comboBox" , "division_num_comboBox" , "main_scope_description_comboBox" , "sub_scope_description_comboBox" , "wbs_Code_comboBox" , "item_description_textBox" , "material_labor_comboBox", "source_dealer_textBox", "invoice_sales_number_textBox" , "quantity_textBox" , "unit_comboBox" , "remarks_textBox" , "unit_cost_metroTextBox" , "total_amount_metroTextBox", "source_dealer_textBox", "invoice_sales_number_textBox" };

            string fields_empty_value_holder = "";

            string date_purchased = "";

            for( int i = 0 ; i < controls_array.Length ; i++ )
            {
                foreach( var item in Controls.Find( controls_array[ i ] , true ) )
                {
                    if( item.Text == controls_text_array[ i ] && item.ForeColor == Color.DarkGray || item.Text == "" )
                    {
                        if( item.Name.Replace( "_" , "" ).ToUpper( ).Replace( item.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).ToUpper( ) == "divisionnum".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "Division no., ".ToUpper( );
                        }
                        else if( item.Name.Replace( "_" , "" ).ToUpper( ).Replace( item.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).ToUpper( ) == "wbscode".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "WBS code, ".ToUpper( );
                        }
                        else if( item.Name.ToUpper( ) == "unit_cost_metroTextBox".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "Unit cost, ".ToUpper( );
                        }
                        else if( item.Name.ToUpper( ) == "total_amount_metroTextBox".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "No total amount, ".ToUpper( );
                        }
                        else
                        {
                            if( item.Name.Replace( "_" , "" ).ToUpper( ).Replace( item.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).ToUpper( ) == "unit".ToUpper( ) )
                            {
                                controls_array[ i ] = "";
                                fields_empty_value_holder += "Unit measurement, ".ToUpper( );
                            }
                            else
                            {
                                controls_array[ i ] = "";
                                fields_empty_value_holder += item.Name.Replace( "_" , " " ).ToUpper( ).Replace( item.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ) + ", ".ToUpper( );
                            }
                        }
                    }
                    else
                    {
                        controls_array[ i ] = item.Text;
                    }
                }
            }
            if( datepurchased_Label.Visible )
            {
                date_purchased = "";
                fields_empty_value_holder += "Date purchased, ".ToUpper( );
            }
            else
            {
                date_purchased = date_purchased_DateTimePicker.Text;
            }

            if( fields_empty_value_holder != string.Empty )
            {
                DialogResult Yes;
                if( controls_array[ 7 ] == "" )
                {
                    Yes = MessageBox.Show( "The following field/'s are empty or no data input :" + Environment.NewLine + Environment.NewLine + fields_empty_value_holder.Remove( fields_empty_value_holder.LastIndexOf( "," ) ) + Environment.NewLine + Environment.NewLine + "YES will save a record with incorrect/missing data that can result to data loss or can't be retreive record (NOT RECOMMENDED), NO to edit (RECOMMENDED)" , "Missing data/'s?" , MessageBoxButtons.YesNoCancel , MessageBoxIcon.Warning );
                }
                else
                {
                    Yes = MessageBox.Show( "The following field/'s are empty or no data input :" + Environment.NewLine + Environment.NewLine + fields_empty_value_holder.Remove( fields_empty_value_holder.LastIndexOf( "," ) ) + Environment.NewLine + Environment.NewLine + "YES to continue and save as " + controls_array[ 7 ].ToUpper( ) + " record with incorrect/missing data that can result to data loss or can't be retreive record (NOT RECOMMENDED), NO to edit (RECOMMENDED), NO to edit" , "Missing data/'s?" , MessageBoxButtons.YesNoCancel , MessageBoxIcon.Warning );
                }

                if( Yes == DialogResult.Yes )
                {
                   
                }
            }
            else
            {
               

            }
        }

        private void get_field_values_navigating_buttons ( ) {
            string[ ] controls_array = new string[ ] { "main_project_comboBox" , "sub_project_comboBox" , "DivisionNum_comboBox" , "main_scope_description_comboBox" , "sub_scope_description_comboBox" , "wbs_Code_comboBox" , "item_description_textBox" , "material_labor_comboBox" , "quantity_textBox" , "unit_comboBox" , "remarks_textBox" , "unit_cost_metroTextBox" , "total_amount_metroTextBox" , "source_dealer_textBox" , "invoice_sales_number_textBox", "vat_exclusive_metroTextBox" , "vat_metroTextBox" };

            foreach( DataGridViewColumn item in journal_datagridView.Columns )
            {
                if( item.Index > 3 )
                {
                    for( int i = 0 ; i < controls_array.Length ; i++ )
                    {
                        foreach( var item_controls in Controls.Find( controls_array[ i ] , true ) )
                        {
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == "DATE_PURCHASED" )
                            {
                                item_controls.Visible = true;
                            }
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == item.Name.ToUpper( ) )
                            {
                                item_controls.ForeColor = Color.Black;
                                item_controls.Text = journal_datagridView.CurrentRow.Cells[ item.Index ].Value.ToString( );
                                date_purchased_DateTimePicker.Text = journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index ].Cells[ "date_purchased" ].Value.ToString( );
                            }                       
                        }
                    }
                }
            }
            datepurchased_Label.Visible = false;
        }

        private void get_field_values_up_arrow ( ) {
            string[ ] controls_array = new string[ ] { "main_project_comboBox" , "sub_project_comboBox" , "DivisionNum_comboBox" , "main_scope_description_comboBox" , "sub_scope_description_comboBox" , "wbs_Code_comboBox" , "item_description_textBox" , "material_labor_comboBox" , "quantity_textBox" , "unit_comboBox" , "remarks_textBox" , "unit_cost_metroTextBox" , "total_amount_metroTextBox" , "source_dealer_textBox" , "invoice_sales_number_textBox","vat_exclusive_metroTextBox","vat_metroTextBox" };

            foreach( DataGridViewColumn item in journal_datagridView.Columns )
            {
                if( item.Index > 3 )
                {
                    for( int i = 0 ; i < controls_array.Length ; i++ )
                    {
                        foreach( var item_controls in Controls.Find( controls_array[ i ] , true ) )
                        {
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == "DATE_PURCHASED" )
                            {
                                item_controls.Visible = true;
                            }
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == item.Name.ToUpper( ) )
                            {
                                item_controls.ForeColor = Color.Black;
                                item_controls.Text = journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index - 1 ].Cells[ item.Index ].Value.ToString( );
                                date_purchased_DateTimePicker.Text = journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index - 1 ].Cells[ "date_purchased" ].Value.ToString( );
                            }                         
                        }
                    }
                }
            }
        }

        public void prev_record ( ) {
            if( current_row > 0 )
            {
                journal_datagridView.Rows[ current_row ].Selected = false;
                journal_datagridView.Rows[ --current_row ].Selected = true;
                journal_datagridView.CurrentCell = journal_datagridView.Rows[ current_row ].Cells[ journal_datagridView.CurrentCell.ColumnIndex ];

                get_field_values_navigating_buttons( );
            }
        }

        public void prev_page ( ) {
            if( start >= 30 && ds.Tables[ 0 ].Rows.Count == 30 )
            { /*Check condition if globalize int start initialized value of zero, is greater than or equal to 30 and Dataset ds row count is equal to 30*/
                start = start - 30; //if true minus 30 int start  until lblPageCount.text turns to 1 equivalent to first page or first 30 records
                if( page_number_toolStripTextBox.Text != "1" )
                {  //check if text in lblPageCount is not equal to 1
                    pageCount = Convert.ToInt32( page_number_toolStripTextBox.Text ) - 1; //if true int pagecount initialized w/ value of 1, convert lblPageCount.text to int value then subract it with value of 1
                    page_number_toolStripTextBox.Text = pageCount.ToString( ); // then convert pagecount to a string value and place it to lblPageCount.Text
                }
                current_row = 0;
            }
            else
            {
                if( page_number_toolStripTextBox.Text != "1" )
                {
                    pageCount = Convert.ToInt32( page_number_toolStripTextBox.Text ) - 1;
                    page_number_toolStripTextBox.Text = pageCount.ToString( );
                }
                if( start != 0 )
                {
                    start = start - 30;
                }
                current_row = 0;
            }
            ds.Clear( );
            adapter.Fill( ds , Convert.ToInt32( start ) , 30 , "bill_mat" );
            current_record_count_toolStripLabel.Text = "Showing " + journal_datagridView.RowCount.ToString( ) + " records of page : ";
        }

        private void get_field_values_down_arrow ( ) {
            string[ ] controls_array = new string[ ] { "main_project_comboBox" , "sub_project_comboBox" , "DivisionNum_comboBox" , "main_scope_description_comboBox" , "sub_scope_description_comboBox" , "wbs_Code_comboBox" , "item_description_textBox" , "material_labor_comboBox" , "quantity_textBox" , "unit_comboBox" , "remarks_textBox" , "unit_cost_metroTextBox" , "total_amount_metroTextBox" , "source_dealer_textBox" , "invoice_sales_number_textBox" , "vat_exclusive_metroTextBox" , "vat_metroTextBox" };

            foreach( DataGridViewColumn item in journal_datagridView.Columns )
            {
                if( item.Index > 3 )
                {
                    for( int i = 0 ; i < controls_array.Length ; i++ )
                    {
                        foreach( var item_controls in Controls.Find( controls_array[ i ] , true ) )
                        {
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == "DATE_PURCHASED" )
                            {
                                item_controls.Visible = true;
                            }
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == item.Name.ToUpper( ) )
                            {
                                item_controls.ForeColor = Color.Black;
                                item_controls.Text = journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index + 1 ].Cells[ item.Index ].Value.ToString( );
                                date_purchased_DateTimePicker.Text = journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index + 1 ].Cells[ "date_purchased" ].Value.ToString( );
                            }
                        }
                    }
                }
            }           
        }

        public void next_record ( ) {
            if( current_row <= journal_datagridView.RowCount - 2 )
            {
                journal_datagridView.Rows[ current_row ].Selected = false;
                journal_datagridView.Rows[ ++current_row ].Selected = true;
                journal_datagridView.CurrentCell = journal_datagridView.Rows[ current_row ].Cells[ journal_datagridView.CurrentCell.ColumnIndex ];
                
                get_field_values_navigating_buttons( );
            }
        }

        public void next_page ( ) {
          
         
        }

        private void Journal_form_Activated(object sender, EventArgs e) {
            if ( new_project_form_count == 1 )
            {
                load_record( );
                total_expense( );
                main_project_comboBox.ForeColor = Color.Black;
                main_project_comboBox.Text = project_selected_str;
                main_project_comboBox.SelectAll( );
                if ( main_project_comboBox.AutoCompleteCustomSource.Count <= 1 )
                {
                    main_project_comboBox.AutoCompleteCustomSource.Clear( );
                    main_project_comboBox.Items.Clear( );
                    complete.Autocomplete_project( main_project_comboBox );
                }
                new_project_form_count = 0;
            }
            Form_opacity_fade.form_opacity_fade_in( this );
        }
      
        private void txtUnitCost_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode==Keys.Enter)
            {
                journal_toolStrip.Focus();
                save_toolStripButton.Select();
            }
        }

        private void close_pictureBox_Click(object sender, EventArgs e) {
            Environment.Exit( Environment.ExitCode );
        }

        private void minimize_pictureBox_Click(object sender, EventArgs e) {
            WindowState = FormWindowState.Minimized;
        }

        private void subproject_comboBox_Leave(object sender, EventArgs e) {
            date_purchased_DateTimePicker.Enabled = true;
            date_purchased_DateTimePicker.Focus();          
        }

        private void dtpDateEntry_ValueChanged(object sender, EventArgs e) {
            date_purchased_DateTimePicker.Visible = false;
        }

        private void tableLayoutPanel1_Paint ( object sender , PaintEventArgs e ) {

        }

        private void main_project_comboBox_SelectedIndexChanged ( object sender , EventArgs e ) {
            sub_project_comboBox.Items.Clear( );// to clear to existing items in sub-project, so that it will not increment everytime selection is made from main project
            complete.Autocomplete_sub_project( main_project_comboBox , sub_project_comboBox );
        }

        private void main_project_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void main_project_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                main_project_comboBox.DroppedDown = true;
            }
        }

        private void main_project_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void DivisionNum_comboBox_SelectionChangeCommitted ( object sender , EventArgs e ) {
            selection_automation bill = new selection_automation( );
            bill.sub_scope_selection( sub_scope_description_comboBox , division_num_comboBox , main_scope_description_comboBox );

        //    Reusable_codes.Autocomplete_source selection = new Reusable_codes.Autocomplete_source( );

            //foreach( var item in selection.Autocomplete_sub_scope_selection_get_items( sub_scope_description_comboBox , DivisionNum_comboBox , main_scope_description_comboBox ) )
            //{
            //    sub_scope_description_comboBox.AutoCompleteCustomSource.Add( item );
            //}
            //sub_scope_description_comboBox.AutoCompleteCustomSource = sub_scope_description_comboBox.AutoCompleteCustomSource;
        }

        private void DivisionNum_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                division_num_comboBox.DroppedDown = true;
            }
            else
            {
                division_num_comboBox.DroppedDown = false;
            }
        }

        private void DivisionNum_comboBox_DropDown ( object sender , EventArgs e ) {
            division_num_comboBox.DataSource = DivcmbItems;
        }

        private void main_scope_description_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                main_scope_description_comboBox.DroppedDown = true;
            }
        }

        private void sub_scope_description_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                sub_scope_description_comboBox.DroppedDown = true;
            }
            else
            {
                sub_scope_description_comboBox.DroppedDown = false;
            }
        }

        private void wbs_Code_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                wbs_Code_comboBox.DroppedDown = true;
            }
        }

        private void material_labor_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                material_labor_comboBox.DroppedDown = true;
            }
        }

        private void unit_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                unit_comboBox.DroppedDown = true;
            }
        }

        private void date_purchased_DateTimePicker_ValueChanged ( object sender , EventArgs e ) {
            datepurchased_Label.Visible = false;
        }

        private void unit_cost_metroTextBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Enter )
            {
                DialogResult vat = MessageBox.Show( "Transaction with VAT?" , "VAT transaction" , MessageBoxButtons.YesNo , MessageBoxIcon.Question );

                Computation compute = new Computation( quantity_textBox , unit_cost_metroTextBox , total_amount_metroTextBox );
                compute.compute_total_amount( );
                vat_exclusive_metroTextBox.ForeColor = Color.Black;
                vat_metroTextBox.ForeColor = Color.Black;
                if( vat == DialogResult.Yes )
                {
                    Computation compute_vat = new Computation( );
                    compute_vat.compute_vat_vat_exclusive( total_amount_metroTextBox , vat_exclusive_metroTextBox , vat_metroTextBox );
                }
                else
                {
                   
                    vat_exclusive_metroTextBox.Text = "0.00";
                    vat_metroTextBox.Text = "0.00";
                }
                journal_toolStrip.Focus( );
                save_toolStripButton.Select( );
                total_amount_metroTextBox.ForeColor = Color.Black;
            }
            #region old codes
            //if( e.KeyCode == Keys.Enter )
            //{
            //    Computation compute = new Computation( quantity_textBox , unit_cost_metroTextBox , total_amount_metroTextBox );
            //    compute.compute_total_amount( );
            //    //   journal_toolStrip.Focus( );
            //    //   save_toolStripButton.Select( );
            //    //panel4.Focus( );
            //    //expense_bunifuCheckbox.Select( );
            //    //label1.BackColor = Color.DodgerBlue;
            //    //label1.ForeColor = Color.White;
            //    total_amount_metroTextBox.ForeColor = Color.Black;
            //}
            #endregion
        }

        private void sub_project_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void sub_project_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void DivisionNum_comboBox_Leave ( object sender , EventArgs e ) {

            string val = "";

            if( division_num_comboBox.Text == string.Empty )// this is to handle the next manual input value of the division number comboBox
            {
                Data_fields_unfocus_setup( );
                val = division_num_comboBox.Text.Remove( 0 , 12 );
            }
            else if( division_num_comboBox.Text.Contains( "Division  - " ) )
            {

            }
            else
            {
                Division_number_inputting_value( );
            }
        }

        private void DivisionNum_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void main_scope_description_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
            if( main_scope_description_comboBox.Enabled == true )
            {
                complete.Autocomplete_sub_scope_description_not_standard( sub_scope_description_comboBox , main_scope_description_comboBox );
            }
        }

        private void main_scope_description_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void sub_scope_description_comboBox_Leave ( object sender , EventArgs e ) {
            try
            {
                bool enable = false;
                Data_fields_unfocus_setup( );
                Autocomplete_source auto = new Autocomplete_source( );

                foreach( var item in auto.Autocomplete_sub_scope_selection_get_items( sub_scope_description_comboBox , division_num_comboBox , main_scope_description_comboBox ) )
                {
                    if( sub_scope_description_comboBox.Text == item )
                    {
                        enable = true;
                        selection_automation g = new selection_automation( );
                        g.wbs_code_selection( division_num_comboBox , wbs_Code_comboBox , sub_scope_description_comboBox , main_scope_description_comboBox );
                    }
                }
                if( enable == false /*&& wbs_Code_comboBox.Enabled == false*/ )
                {
                    wbs_Code_comboBox.Enabled = true;
                    wbs_Code_comboBox.Text = "-Select WBS code-";
                    wbs_Code_comboBox.ForeColor = Color.DarkGray;

                    complete.Autocomplete_wbs_code_not_standard( wbs_Code_comboBox , sub_scope_description_comboBox );
                }
            }
            catch( Exception ex )
            {
                MessageBox.Show( ex.Message );
            }
          
        }

        private void sub_scope_description_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
            if( main_scope_description_comboBox.Enabled == true )
            {
                complete.Autocomplete_sub_scope_description_not_standard( sub_scope_description_comboBox , main_scope_description_comboBox );
            }
        }

        private void wbs_Code_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void wbs_Code_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void item_description_textBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void item_description_textBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void material_labor_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void material_labor_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void source_dealer_textBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void source_dealer_textBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void invoice_num_textBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );         
        }

        private void invoice_num_textBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void remarks_textBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void remarks_textBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void quantity_textBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void quantity_textBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void unit_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );            
        }

        private void unit_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void unit_cost_metroTextBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void unit_cost_metroTextBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void material_labor_comboBox_Leave_1 ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void material_labor_comboBox_Enter_1 ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void removeProject_ToolStripMenuItem_Click ( object sender, EventArgs e ) {
            New_project_form New = new New_project_form( );
            New.Add_toolStripButton.Visible = false;
            New.toolStripSeparator11.Visible = false;
            New.ShowDialog( );
        }

        private void print_toolStripButton_Click ( object sender , EventArgs e ) {
            Project_Price_Form.Project_price_Form fm = new Project_Price_Form.Project_price_Form( );
            fm.ShowDialog( );
            print_journal = true;
            Reports.Dashboard.Dashboard_form form = new Reports.Dashboard.Dashboard_form( );
            form.ShowDialog( );
        }

        private void material_labor_comboBox_KeyDown_1 ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                material_labor_comboBox.DroppedDown = true;
            }
        }

        private void panel3_Paint ( object sender , PaintEventArgs e ) {

        }

        private void expense_bunifuCheckbox_OnChange ( object sender , EventArgs e ) {

            //revenue_bunifuCheckbox.Checked = false;
           
        }

        private void revenue_bunifuCheckbox_OnChange ( object sender , EventArgs e ) {
            //expense_bunifuCheckbox.Checked = false;
        }

        private void sub_project_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                sub_project_comboBox.DroppedDown = true;
            }
            else
            {
                sub_project_comboBox.DroppedDown = false;
            }
        }

        private void sub_scope_description_comboBox_SelectionChangeCommitted_1 ( object sender , EventArgs e ) {
            try
            {
                selection_automation g = new selection_automation( );
                g.wbs_code_selection( division_num_comboBox , wbs_Code_comboBox , sub_scope_description_comboBox , main_scope_description_comboBox );
            }
            catch( Exception ex )
            {
                MessageBox.Show( ex.Message );
            }           
        }

        private void journal_datagridView_CellValueChanged ( object sender , DataGridViewCellEventArgs e ) {
            foreach( DataGridViewRow item in journal_datagridView.Rows )
            {
                bool check = Convert.ToBoolean( item.Cells[ 0 ].Value );
                if( check == true )
                {
                    item.Selected = true;
                }
            }
        }

        private void journal_datagridView_CurrentCellDirtyStateChanged ( object sender , EventArgs e ) {
            if( journal_datagridView.IsCurrentCellDirty )
            {
                journal_datagridView.CommitEdit( DataGridViewDataErrorContexts.Commit );
            }
        }

        private void journal_datagridView_CellClick ( object sender , DataGridViewCellEventArgs e ) {
            string[ ] controls_array = new string[ ] { "main_project_comboBox" , "sub_project_comboBox" , "division_num_comboBox" , "main_scope_description_comboBox" , "sub_scope_description_comboBox" , "wbs_Code_comboBox" , "item_description_textBox" , "material_labor_comboBox" , "quantity_textBox" , "unit_comboBox" , "remarks_textBox" , "unit_cost_metroTextBox" , "total_amount_metroTextBox" , "source_dealer_textBox" , "invoice_sales_number_textBox" , "vat_exclusive_metroTextBox" , "vat_metroTextBox" };
            current_row = journal_datagridView.CurrentRow.Index;
            foreach( DataGridViewColumn item in journal_datagridView.Columns )
            {
                if( item.Index > 3 )
                {
                    for( int i = 0 ; i < controls_array.Length ; i++ )
                    {
                        foreach( var item_controls in Controls.Find( controls_array[ i ] , true ) )
                        {                     
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == item.Name.ToUpper( ) )
                            {
                                item_controls.ForeColor = Color.Black;
                                item_controls.Text = journal_datagridView.CurrentRow.Cells[ item.Index ].Value.ToString( );
                                date_purchased_DateTimePicker.Text = journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index ].Cells[ "date_purchased" ].Value.ToString( );
                          
                            }
                        }
                    }
                }
            }
            datepurchased_Label.Visible = false;
        }

        private void invoice_number_textBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void prev_page_toolStripButton_Click ( object sender , EventArgs e ) {
            prev_page( );
        }

        private void next_page_toolStripButton_Click ( object sender , EventArgs e ) {
            next_page( );
        }

        private void next_record_toolStripButton_Click ( object sender , EventArgs e ) {
            next_record( );
        }

        private void prev_record_toolStripButton_Click ( object sender , EventArgs e ) {
            prev_record( );
        }

        private void journal_datagridView_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Left )
            {
                prev_page( );
            }
            if( e.KeyCode == Keys.Right )
            {
                next_page( );
            }
            if( e.KeyCode == Keys.Up )
            {
                if( current_row > 0 )
                {
                    journal_datagridView.Rows[ current_row ].Selected = false;
                    journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index ].Selected = true;

                    journal_datagridView.CurrentCell = journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index ].Cells[ journal_datagridView.CurrentCell.ColumnIndex ];
                    current_row--;
                    get_field_values_up_arrow( );
                }
            }
            if( e.KeyCode == Keys.Down )
            {
                if( current_row <= journal_datagridView.RowCount - 2 )
                {
                    journal_datagridView.Rows[ current_row ].Selected = false;
                    journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index ].Selected = true;

                    journal_datagridView.CurrentCell = journal_datagridView.Rows[ journal_datagridView.CurrentRow.Index ].Cells[ journal_datagridView.CurrentCell.ColumnIndex ];
                    current_row++;
                    get_field_values_down_arrow( );
                }
            }
        }

        private void edit_toolStripButton_Click ( object sender , EventArgs e ) {
            string[ ] controls_text_array = new string[ ] { "-Select main project-" , "-Select sub-project-" , "-Select division no.-" , "-Select main scope description-" , "-Select sub-scope description-" , "-Select WBS code-" , "Item description" , "-Select material/labor-" , "Source/Dealer" , "Invoice/Sales no." , "Quantity" , "-Select unit measurement-" , "Remarks" , "Unit cost" , "Total amount" , "Source/Dealer" , "Invoice/Sales no." };

            string[ ] controls_array = new string[ ] { "main_project_comboBox" , "sub_project_comboBox" , "division_num_comboBox" , "main_scope_description_comboBox" , "sub_scope_description_comboBox" , "wbs_Code_comboBox" , "item_description_textBox" , "material_labor_comboBox" , "quantity_textBox" , "unit_comboBox" , "remarks_textBox" , "unit_cost_metroTextBox" , "total_amount_metroTextBox" , "source_dealer_textBox" , "invoice_sales_number_textBox" , "vat_exclusive_metroTextBox" , "vat_metroTextBox" };

            string fields_empty_value_holder = "";

            string date_purchased = "";

            for( int i = 0 ; i < controls_array.Length ; i++ )
            {
                foreach( var item in Controls.Find( controls_array[ i ] , true ) )
                {
                    if( item.Text == controls_text_array[ i ] && item.ForeColor == Color.DarkGray || item.Text == "" )
                    {
                        if( item.Name.Replace( "_" , "" ).ToUpper( ).Replace( item.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).ToUpper( ) == "divisionnum".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "Division no., ".ToUpper( );
                        }
                        else if( item.Name.Replace( "_" , "" ).ToUpper( ).Replace( item.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).ToUpper( ) == "wbscode".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "WBS code, ".ToUpper( );
                        }
                        else if( item.Name.ToUpper( ) == "unit_cost_metroTextBox".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "Unit cost, ".ToUpper( );
                        }
                        else if( item.Name.ToUpper( ) == "total_amount_metroTextBox".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "No total amount, ".ToUpper( );
                        }
                        else if( item.Name.ToUpper( ) == "vat_exclusive_metroTextBox".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "No Vat exclusive amount, ".ToUpper( );
                        }
                        else if( item.Name.ToUpper( ) == "vat_metroTextBox".ToUpper( ) )
                        {
                            controls_array[ i ] = "";
                            fields_empty_value_holder += "No Vat amount, ".ToUpper( );
                        }
                        else
                        {
                            if( item.Name.Replace( "_" , "" ).ToUpper( ).Replace( item.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).ToUpper( ) == "unit".ToUpper( ) )
                            {
                                controls_array[ i ] = "";
                                fields_empty_value_holder += "Unit measurement, ".ToUpper( );
                            }
                            else
                            {
                                controls_array[ i ] = "";
                                fields_empty_value_holder += item.Name.Replace( "_" , " " ).ToUpper( ).Replace( item.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ) + ", ".ToUpper( );
                            }
                        }
                    }
                    else
                    {
                        controls_array[ i ] = item.Text;
                    }
                }
            }
            if( datepurchased_Label.Visible )
            {
                date_purchased = "";
                fields_empty_value_holder += "Date purchased, ".ToUpper( );
            }
            else
            {
                date_purchased = date_purchased_DateTimePicker.Text;
            }
            if( fields_empty_value_holder != string.Empty )
            {
                DialogResult Yes;
                if( controls_array[ 7 ] == "" )// this is array 7, or material/labor data field
                {
                    Yes = MessageBox.Show( "The following field/'s are empty or no data input :" + Environment.NewLine + Environment.NewLine + fields_empty_value_holder.Remove( fields_empty_value_holder.LastIndexOf( "," ) ) + Environment.NewLine + Environment.NewLine + "YES will update a record with incorrect/missing data that can result to data loss or can't be retreive record (NOT RECOMMENDED), NO to edit (RECOMMENDED)" , "Missing data/'s?" , MessageBoxButtons.YesNoCancel , MessageBoxIcon.Warning );
                }
                else
                {
                    Yes = MessageBox.Show( "The following field/'s are empty or no data input :" + Environment.NewLine + Environment.NewLine + fields_empty_value_holder.Remove( fields_empty_value_holder.LastIndexOf( "," ) ) + Environment.NewLine + Environment.NewLine + "YES to continue and update as " + controls_array[ 7 ].ToUpper( ) + " record with incorrect/missing data that can result to data loss or can't be retreive record (NOT RECOMMENDED), NO to edit (RECOMMENDED), NO to edit" , "Missing data/'s?" , MessageBoxButtons.YesNoCancel , MessageBoxIcon.Warning );
                }

                if( Yes == DialogResult.Yes )
                {
                 
               
                }
            }
            else
            {
            
            
            }
        }

        private void refresh_toolStripButton_Click ( object sender , EventArgs e ) {
            refresh_control( );
            load_record( );
            total_expense( );
        }

        private void remove_toolStripButton_Click ( object sender , EventArgs e ) {
            DialogResult Yes; int count = 0;
            foreach( var item in journal_datagridView.Rows.Cast<DataGridViewRow>( ).ToArray( ) )
            {
                bool checkbox = Convert.ToBoolean( item.Cells[ 0 ].Value );
                if( checkbox == true )
                {
                    count = 1;
                }
            }
            if( count == 0 )
            {
                MessageBox.Show( "NO selected record/'s to be removed, please select!" , "No selection" , MessageBoxButtons.OK , MessageBoxIcon.Exclamation );
            }
            else
            {
                Yes = MessageBox.Show( "The selected records will be remove and move to archives, are you sure you want to remove the selected records?" , "remove records" , MessageBoxButtons.YesNoCancel , MessageBoxIcon.Warning );
                if( Yes == DialogResult.Yes )
                {
                  
                }
            }
        }

        private void search_metroTextBox_Click ( object sender , EventArgs e ) {
            if( search_metroTextBox.Text == "Search" )
            {
                search_metroTextBox.Clear( );
                search_metroTextBox.ForeColor = Color.Black;
            }
        }

        private void search_metroTextBox_Leave ( object sender , EventArgs e ) {
            if( search_metroTextBox.Text == string.Empty )
            {
                search_metroTextBox.ForeColor = Color.DarkGray;
                search_metroTextBox.Text = "Search";
                load_record( );
            }
        }

        private void search_metroTextBox_TextChanged ( object sender , EventArgs e ) {
           
        }

        private void expense_bunifuCheckbox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode==Keys.Enter )
            {
                //expense_bunifuCheckbox.Checked = true;
                //revenue_bunifuCheckbox.Checked = false;
            }
            else if ( e.Control && e.KeyCode==Keys.S )
            {
                save_toolStripButton_Click( this, new EventArgs());
            }
        }

        private void revenue_bunifuCheckbox_KeyDown ( object sender, KeyEventArgs e ) {
            if ( e.KeyCode == Keys.Enter )
            {                
                //revenue_bunifuCheckbox.Checked = true;
                //expense_bunifuCheckbox.Checked = false;
            }
            else if ( e.Control && e.KeyCode == Keys.S )
            {
                save_toolStripButton_Click( this, new EventArgs( ) );
            }
        }

        private void expense_bunifuCheckbox_Leave ( object sender, EventArgs e ) {
            //label1.ForeColor = Color.Black;
            //label1.BackColor = Color.White;
        }

        private void revenue_bunifuCheckbox_Enter ( object sender, EventArgs e ) {
            //label2.ForeColor = Color.White;
            //label2.BackColor = Color.DodgerBlue;
        }

        private void expense_bunifuCheckbox_Enter ( object sender, EventArgs e ) {
            //label1.ForeColor = Color.White;
            //label1.BackColor = Color.DodgerBlue;
        }

        private void revenue_bunifuCheckbox_Leave ( object sender, EventArgs e ) {
            //label2.ForeColor = Color.Black;
            //label2.BackColor = Color.White;
        }
        Backup_db back = new Backup_db( );
        private void project_toolStripDropDownButton_Click ( object sender , EventArgs e ) {
            Project_Selection.Project_selection_form select = new Project_Selection.Project_selection_form( );
            select.ShowDialog( );
        }

        private void records_backuptoolStripButton_Click ( object sender , EventArgs e ) {
            back.export( Fields_reusable_base_class.journal_table , Name );
            //Backup_database.Export ex = new Backup_database.Export( );
            //ex.backup_records("journal");
        }

        private void search_metroTextBox_KeyDown ( object sender, KeyEventArgs e ) {
            if ( e.KeyCode==Keys.Enter )
            {
                search_records( );
            }
        }

        private void home_toolStripButton_Click ( object sender , EventArgs e ) {
            try
            {
                if( Application.OpenForms.OfType<Home_Form>( ).Count( ) == 1 )
                {
                    timer_Fadeout.Start( );
                }
                else
                {
                    Home_form = new Home_Form( );
                    Home_form.FormClosed += Home_form_FormClosed;
                    Home_form.Show( );
                }
            }
            catch( Exception ex )
            {

                MessageBox.Show( "Error! : " + ex.Message , "ERROR!" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Error );
            }
        }

        private void backgroundWorker1_DoWork ( object sender , DoWorkEventArgs e ) {
           // search_records( );
        }

        private void import_ToolStripMenuItem_Click ( object sender , EventArgs e ) {
           // Backup_database.Import imp = new Backup_database.Import( );
            if( openFileDialog1.ShowDialog( ) == DialogResult.OK )
            {
               back.import( Fields_reusable_base_class.journal_table);
            }
        }

        private void Journal_form_Deactivate( object sender , EventArgs e ) {
            Form_opacity_fade.form_opacity_fade( this );
        }
    }
}
