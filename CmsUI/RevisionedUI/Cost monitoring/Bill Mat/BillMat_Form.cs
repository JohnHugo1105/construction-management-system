using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using MySql.Data.MySqlClient;
using GSG_Builders.RevisionedUI.Home;
using GSG_Builders.RevisionedUI.Reusable_codes;
using System.Speech.Synthesis;
using System.Data.SqlClient;
using Microsoft.SqlServer.Management.Smo;

namespace GSG_Builders.RevisionedUI.Bill_Mat {
    public partial class BillMat_Form : Form {
      
        Autocomplete_source complete = new Autocomplete_source( );

        Control active_control;//this is to use for the focus and leave events of the controls for record entry inputs
                                        /// <summary>
                                        /// /Sets the hint texts of the active controls
                                        /// </summary>

       List<string> DivcmbItems = new List<string>();//this is to get the default items in the comboBox of Division no.

        public static  string project_selected_str = "";//wil hold the value name of the selected project

        int current_row = 0;

     //   double offset = 30;

      public static  int new_project_form_count = 0;

        public static bool print_bill_mat = false;

        public BillMat_Form() {
            InitializeComponent();
            complete.Autocomplete_project( main_project_comboBox );
            complete.Autocomplete_sub_project( main_project_comboBox , sub_project_comboBox );
            Autocomplete( );

              

            checkbox_column();//Checkbox column

         
            
            foreach( string item in division_num_comboBox.Items )
            {
                DivcmbItems.Add( item );//adding the default items in Division no. combobox          
            }
        }
        private void Autocomplete( ) {
            /* sets all the autocomplete source of controls */
            //complete.Autocomplete_view_project_records_toolStripComboBox( project_selected_str );
           
            complete.Autocomplete_main_scope_description( main_scope_description_comboBox );
            complete.Autocomplete_sub_scope_description( sub_scope_description_comboBox );
            complete.Autocomplete_item_description( item_description_textBox );
            complete.Autocomplete_unit_measurement( unit_comboBox );
            /* ends here */
        }
        private void Autocomplete_clear( ) {
            //main_project_comboBox.AutoCompleteCustomSource.Clear( );
            //    sub_project_comboBox.AutoCompleteCustomSource.Clear( );
            main_scope_description_comboBox.AutoCompleteCustomSource.Clear( );
            sub_scope_description_comboBox.AutoCompleteCustomSource.Clear( );
            item_description_textBox.AutoCompleteCustomSource.Clear( );
            unit_comboBox.AutoCompleteCustomSource.Clear( );
        }
        private void checkbox_column() {
            datagridview_configuration checkbox = new datagridview_configuration( bill_mat_datagridView);
            checkbox.checkboxColumn();
        }

        SqlDataAdapter adapter = new SqlDataAdapter( );
        DataSet ds = new DataSet();
      
        private void refresh_toolStripButton_Click(object sender, EventArgs e) {
           
        }           
      
        private void DivisionNum_cmb_DropDown(object sender, EventArgs e) {
            division_num_comboBox.DataSource = DivcmbItems;
        }
        
        private void save_toolStripButton_Click(object sender, EventArgs e) {

        
        }

        private void edit_toolStripButton_Click(object sender, EventArgs e) {

        
        }

        private void main_project_comboBox_SelectedIndexChanged(object sender, EventArgs e) {
            sub_project_comboBox.Items.Clear();// to clear to existing items in sub-project, so that it will not increment everytime selection is made from main project
            complete.Autocomplete_sub_project(main_project_comboBox, sub_project_comboBox);
        }

        private void date_purchased_dtp_ValueChanged(object sender, EventArgs e) {
            datepurchased_Label.Visible = false;
        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e) {
           
        }
        
        private void unit_cost_metroTextBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode==Keys.Enter)
            {
                DialogResult vat = MessageBox.Show( "Transaction with VAT?","VAT transaction",MessageBoxButtons.YesNo,MessageBoxIcon.Question );

                Computation compute = new Computation( quantity_textBox , unit_cost_metroTextBox , total_amount_metroTextBox );
                compute.compute_total_amount( );

                if( vat==DialogResult.Yes )
                {
                    Computation compute_vat = new Computation( );
                    compute_vat.compute_vat_vat_exclusive( total_amount_metroTextBox , vat_exclusive_metroTextBox , vat_metroTextBox );
                }
                else
                {
                    vat_exclusive_metroTextBox.ForeColor = Color.Black;
                    vat_metroTextBox.ForeColor = Color.Black;
                    vat_exclusive_metroTextBox.Text = "0.00";
                    vat_metroTextBox.Text = "0.00";                    
                }
                BillMat_toolStrip.Focus( );
                save_toolStripButton.Select( );

                total_amount_metroTextBox.ForeColor = Color.Black;
            }
        }

        #region controls_name fields
        private static string main_project_str = "main_project_comboBox";
        private static string sub_project_str = "sub_project_comboBox";
        private static string DivisionNum_comboBox_str = "division_num_comboBox";
        private static string wbs_code_str = "wbs_Code_comboBox";
        private static string main_scope_description_comboBox_str = "main_scope_description_comboBox";
        private static string material_item_comboBox_str = "material_labor_comboBox";
        private static string sub_scope_description_comboBox_str = "sub_scope_description_comboBox";
        private static string item_description_textBox_str = "item_description_textBox";
        private static string remarks_textBox_str = "remarks_textBox";
        private static string quantity_txt_str = "quantity_textBox";
        private static string unit_txt_str = "unit_comboBox";
        #endregion

        private void Data_fields_unfocus_setup() {
           
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
                        active_control.Text=  "-Select division no.-";
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
        private void Data_fields_focus_setup() {
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
        private void refresh_control ( ) {
            save_refresh_controls clear = new save_refresh_controls( );
            clear.Clear_text_bill_mat( main_project_comboBox , sub_project_comboBox , division_num_comboBox , wbs_Code_comboBox , main_scope_description_comboBox , material_labor_comboBox , sub_scope_description_comboBox , item_description_textBox , remarks_textBox , quantity_textBox , unit_comboBox , unit_cost_metroTextBox,total_amount_metroTextBox , datepurchased_Label,vat_exclusive_metroTextBox,vat_metroTextBox );

            sub_scope_description_comboBox.AutoCompleteCustomSource.Clear( );
            complete.Autocomplete_sub_scope_description( sub_scope_description_comboBox );

            sub_scope_description_comboBox.Text = "-Select sub-scope description-";
        }

        private void search_records ( ) {
            if ( search_metroTextBox.Text == "Search" )
            {
            
                next_page_toolStripButton.Enabled = true;
                prev_page_toolStripButton.Enabled = true;
            }
            else
            {
              
            }
        }

        private void form_activated( ) {
            if( new_project_form_count == 1 )
            {                
           
                main_project_comboBox.ForeColor = Color.Black;
                main_project_comboBox.Text = project_selected_str;              
                //main_project_comboBox.SelectAll( );
                main_project_comboBox.Focus( );

                if( main_project_comboBox.AutoCompleteCustomSource.Count <= 1 )
                {
                    main_project_comboBox.AutoCompleteCustomSource.Clear( );
                    main_project_comboBox.Items.Clear( );
                    complete.Autocomplete_project( main_project_comboBox );
                }
                new_project_form_count = 0;
            }
            Form_opacity_fade.form_opacity_fade_in( this );      
        }
        private void BillMat_Form_Activated(object sender, EventArgs e) {         
            form_activated( );
        }
      
        private void DivisionNum_comboBox_SelectionChangeCommitted(object sender, EventArgs e) {
            selection_automation bill = new selection_automation();
            bill.sub_scope_selection(sub_scope_description_comboBox, division_num_comboBox, main_scope_description_comboBox);

            //Reusable_codes.Autocomplete_source selection = new Reusable_codes.Autocomplete_source( );

            //foreach( var item in selection.Autocomplete_sub_scope_selection_get_items( sub_scope_description_comboBox , DivisionNum_comboBox , main_scope_description_comboBox ) )
            //{
            //    sub_scope_description_comboBox.AutoCompleteCustomSource.Add( item );
            //}
  
           //sub_scope_description_comboBox.AutoCompleteCustomSource = sub_scope_description_comboBox.AutoCompleteCustomSource;         
        }      
     
        private void newProjectToolStripMenuItem_Click ( object sender , EventArgs e ) {
            New_project_form proj = new New_project_form( );
            proj.ShowDialog( );
        }

        private void DivisionNum_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode==Keys.Down || e.KeyCode==Keys.Up)
            {
                division_num_comboBox.DroppedDown = true;
            }
            else
            {
                division_num_comboBox.DroppedDown = false;
            }
        }

        private void unit_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                unit_comboBox.DroppedDown = true;
            }
        }
      
        private void main_project_comboBox_Leave_1 ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void sub_project_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }
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
        private void DivisionNum_comboBox_Leave ( object sender , EventArgs e ) {
                        
            string val = "";

            if( division_num_comboBox.Text==string.Empty )// this is to handle the next manual input value of the division number comboBox
            {
                Data_fields_unfocus_setup( );
                val = division_num_comboBox.Text.Remove( 0 , 12 );               
            }
            else if( division_num_comboBox.Text.Contains("Division  - ") )
            {
                
            }
            else
            {                
                Division_number_inputting_value( );                
            }         
        }

        private void main_scope_description_comboBox_Leave ( object sender , EventArgs e ) {
            //Data_fields_unfocus_setup(  );
            //if( main_scope_description_comboBox.Enabled==true )
            //{
            //    complete.Autocomplete_sub_scope_description_not_standard( sub_scope_description_comboBox , main_scope_description_comboBox );
            //}    
        }       

        private void wbsCode_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup(  );            
        }

        private void item_description_textBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup(  );
        }

        private void material_labor_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup(  );
        }

        private void quantity_txt_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void unit_comboBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup(  );
        }

        private void remarks_textBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void unit_cost_metroTextBox_Leave ( object sender , EventArgs e ) {
            Data_fields_unfocus_setup(  );
        }

        private void main_project_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void sub_project_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup(  );
        }

        private void DivisionNum_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup(  );
        }

        private void main_scope_description_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup(  );
        }
      
        private void wbsCode_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup(  );
        }

        private void item_description_textBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup(  );
        }

        private void material_labor_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup(  );
        }

        private void quantity_txt_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }

        private void unit_comboBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup(  );
        }

        private void remarks_textBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup(  );
        }

        private void unit_cost_metroTextBox_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
        }     

        private void main_project_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                main_project_comboBox.DroppedDown = true;
            }
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

        private void main_scope_description_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode == Keys.Down || e.KeyCode == Keys.Up )
            {
                main_scope_description_comboBox.DroppedDown = true;
            }
        }

        private void wbsCode_comboBox_KeyDown ( object sender , KeyEventArgs e ) {
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
        private void bill_mat_datagridView_CellClick ( object sender, DataGridViewCellEventArgs e ) {
            get_field_values_navigating_buttons( );
            #region same code
            //string[ ] controls_array = new string[ ] { "main_project_comboBox", "sub_project_comboBox", "division_num_comboBox" , "main_scope_description_comboBox", "sub_scope_description_comboBox", "wbs_Code_comboBox", "item_description_textBox", "date_purchased_DateTimePicker", "material_labor_comboBox", "quantity_textBox", "unit_comboBox", "remarks_textBox", "unit_cost_metroTextBox", "total_amount_metroTextBox", "vat_exclusive_metroTextBox" , "vat_metroTextBox" };
            //current_row = bill_mat_datagridView.CurrentRow.Index;
            //foreach ( DataGridViewColumn item in bill_mat_datagridView.Columns )
            //{  
            //    if (  item.Index>3 )
            //    {
            //        for ( int i = 0 ; i < controls_array.Length ; i++ )
            //        {                       
            //            foreach ( var item_controls in Controls.Find( controls_array[ i ], true ) )
            //            {
            //                if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == "DATE_PURCHASED" )
            //                {                                
            //                    //item_controls.Visible=true;
            //                    //Console.WriteLine( item.Name );
            //                }
            //                if ( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).LastIndexOf( "_" ) ) == item.Name.ToUpper( ) )
            //                {                               
            //                    item_controls.ForeColor = Color.Black;
            //                    item_controls.Text = bill_mat_datagridView.CurrentRow.Cells[ item.Index ].Value.ToString( );
            //                    date_purchased_DateTimePicker.Text = bill_mat_datagridView.Rows[ bill_mat_datagridView.CurrentRow.Index ].Cells[ "date_purchased" ].Value.ToString( );                       
            //                }                          
            //            }                      
            //        }               
            //    }
            //}
            //datepurchased_Label.Visible = false;
            #endregion
        }

        private void search_metroTextBox_TextChanged ( object sender , EventArgs e ) {
            //backgroundWorker1.RunWorkerAsync( );
            //if ( search_metroTextBox.Text == "Search" )
            //{
            //    load_record( );
            //    next_page_toolStripButton.Enabled = true;
            //    prev_page_toolStripButton.Enabled = true;
            //}
            //else
            //{
            //    next_page_toolStripButton.Enabled = false;
            //    prev_page_toolStripButton.Enabled = false;
            //    Reusable_codes.Search search = new Reusable_codes.Search( );
            //    search.bill_mat_search( bill_mat_datagridView, search_metroTextBox, project_selected_str, prev_page_toolStripButton, next_page_toolStripButton, current_record_count_toolStripLabel, page_number_toolStripTextBox, page_count_toolStripLabel, pageCount, current_row );
            //}
        }

        private void search_metroTextBox_Click ( object sender , EventArgs e ) {
            if( search_metroTextBox.Text=="Search" )
            {
                search_metroTextBox.Clear( );
                search_metroTextBox.ForeColor = Color.Black;
            }
           
        }

        private void search_metroTextBox_Leave ( object sender , EventArgs e ) {
            if ( search_metroTextBox.Text==string.Empty )
            {
                search_metroTextBox.ForeColor = Color.DarkGray;
                search_metroTextBox.Text = "Search";
           
            }           
        }

        private void remove_toolStripButton_Click ( object sender , EventArgs e ) {
            DialogResult Yes; int count = 0;
            foreach( var item in bill_mat_datagridView.Rows.Cast<DataGridViewRow>( ).ToArray( ) )
            {
                bool checkbox = Convert.ToBoolean( item.Cells[ 0 ].Value );
                if( checkbox == true )
                {                    
                    count = 1;
                }
            }
            if( count==0 )
            {
                MessageBox.Show( "NO selected record/'s to be removed, please select!" , "No selection" , MessageBoxButtons.OK , MessageBoxIcon.Exclamation );
            }
            else
            {
                Yes = MessageBox.Show( "The selected records will be remove and move to archives, are you sure you want to remove the selected records?" , "remove records" , MessageBoxButtons.YesNoCancel , MessageBoxIcon.Warning );
                if( Yes == DialogResult.Yes )
                {
                    Remove_record remove = new Remove_record( );
                
                }
            }           
        }

        private void comboBox1_SelectionChangeCommitted ( object sender , EventArgs e ) {
          
        }
              
        private void comboBox1_Leave ( object sender , EventArgs e ) {
        
        }
        
        private void comboBox1_KeyDown ( object sender , KeyEventArgs e ) {
        
          
        }

        private void comboBox1_Enter ( object sender , EventArgs e ) {
            Data_fields_focus_setup( );
            if( main_scope_description_comboBox.Enabled == true )
            {
                complete.Autocomplete_sub_scope_description_not_standard( sub_scope_description_comboBox , main_scope_description_comboBox );
            }          
        }

        private void toolStrip1_ItemClicked ( object sender , ToolStripItemClickedEventArgs e ) {

        }
        double start=0;
        int pageCount = 1;
        private void next_page_toolStripButton_Click ( object sender , EventArgs e ) {            
          
        }
      

        private void bill_mat_datagridView_CellValueChanged ( object sender , DataGridViewCellEventArgs e ) {
            foreach( DataGridViewRow item in bill_mat_datagridView.Rows )
            {
                bool check = Convert.ToBoolean( item.Cells[ 0 ].Value );
                if( check ==true )
                {
                    item.Selected = true;
                }
            }
        }

        private void bill_mat_datagridView_CurrentCellDirtyStateChanged ( object sender , EventArgs e ) {
            if( bill_mat_datagridView.IsCurrentCellDirty )
            {
                bill_mat_datagridView.CommitEdit( DataGridViewDataErrorContexts.Commit );
            }
        }
        private void get_field_values_navigating_buttons ( ) {
            string[ ] controls_array = new string[ ] { "main_project_comboBox" , "sub_project_comboBox" , "division_num_comboBox" , "main_scope_description_comboBox" , "sub_scope_description_comboBox" , "wbs_Code_comboBox" , "item_description_textBox" , "date_purchased_DateTimePicker" , "material_labor_comboBox" , "quantity_textBox" , "unit_comboBox" , "remarks_textBox" , "unit_cost_metroTextBox" , "total_amount_metroTextBox","vat_exclusive_metroTextBox","vat_metroTextBox" };
            current_row = bill_mat_datagridView.CurrentRow.Index;
            foreach( DataGridViewColumn item in bill_mat_datagridView.Columns )
            {
                if( item.Index > 3 )
                {
                    for( int i = 0 ; i < controls_array.Length ; i++ )
                    {
                        foreach( var item_controls in Controls.Find( controls_array[ i ] , true ) )
                        {
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == "DATE_PURCHASED" )
                            {
                                //item_controls.Visible=true;
                                //Console.WriteLine( item.Name );
                            }
                            if( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0 , 21 ).ToUpper( ) , "" ).LastIndexOf( "_" ) ) == item.Name.ToUpper( ) )
                            {
                                item_controls.ForeColor = Color.Black;
                                item_controls.Text = bill_mat_datagridView.CurrentRow.Cells[ item.Index ].Value.ToString( );
                                date_purchased_DateTimePicker.Text = bill_mat_datagridView.Rows[ bill_mat_datagridView.CurrentRow.Index ].Cells[ "date_purchased" ].Value.ToString( );
                            }
                        }
                    }
                }
            }
            datepurchased_Label.Visible = false;
        }
        private void get_field_values_up_arrow ( ) {
            string[ ] controls_array = new string[ ] { "main_project_comboBox", "sub_project_comboBox", "division_num_comboBox" , "main_scope_description_comboBox", "sub_scope_description_comboBox", "wbs_Code_comboBox", "item_description_textBox", "date_purchased_DateTimePicker", "material_labor_comboBox", "quantity_textBox", "unit_comboBox", "remarks_textBox", "unit_cost_metroTextBox", "total_amount_metroTextBox","vat_exclusive_metroTextBox","vat_metroTextBox" };

            foreach ( DataGridViewColumn item in bill_mat_datagridView.Columns )
            {
                if ( item.Index > 3 )
                {
                    for ( int i = 0 ; i < controls_array.Length ; i++ )
                    {
                        foreach ( var item_controls in Controls.Find( controls_array[ i ], true ) )
                        {
                            if ( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).LastIndexOf( "_" ) ) == "DATE_PURCHASED" )
                            {
                                item_controls.Visible = true;
                            }
                            if ( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).LastIndexOf( "_" ) ) == item.Name.ToUpper( ) )
                            {
                                item_controls.ForeColor = Color.Black;
                                item_controls.Text = bill_mat_datagridView.Rows[ bill_mat_datagridView.CurrentRow.Index - 1 ].Cells[ item.Index ].Value.ToString( );
                            }
                        }
                    }
                }
            }
        }
        private void get_field_values_down_arrow ( ) {
            string[ ] controls_array = new string[ ] { "main_project_comboBox", "sub_project_comboBox", "division_num_comboBox" , "main_scope_description_comboBox", "sub_scope_description_comboBox", "wbs_Code_comboBox", "item_description_textBox", "date_purchased_DateTimePicker", "material_labor_comboBox", "quantity_textBox", "unit_comboBox", "remarks_textBox", "unit_cost_metroTextBox", "total_amount_metroTextBox" , "vat_exclusive_metroTextBox" , "vat_metroTextBox" };

            foreach ( DataGridViewColumn item in bill_mat_datagridView.Columns )
            {
                if ( item.Index > 3 )
                {
                    for ( int i = 0 ; i < controls_array.Length ; i++ )
                    {
                        foreach ( var item_controls in Controls.Find( controls_array[ i ], true ) )
                        {
                            if ( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).LastIndexOf( "_" ) ) == "DATE_PURCHASED" )
                            {
                                item_controls.Visible = true;
                            }
                            if ( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).Remove( item_controls.Name.ToUpper( ).Replace( item_controls.GetType( ).ToString( ).Remove( 0, 21 ).ToUpper( ), "" ).LastIndexOf( "_" ) ) == item.Name.ToUpper( ) )
                            {
                                item_controls.ForeColor = Color.Black;
                                item_controls.Text = bill_mat_datagridView.Rows[ bill_mat_datagridView.CurrentRow.Index + 1 ].Cells[ item.Index ].Value.ToString( );
                            }
                        }
                    }
                }
            }
        }
        private void move_next_toolStripButton_Click ( object sender , EventArgs e ) {
            next_record( );
        }
        public void next_record ( ) {
            if ( current_row <= bill_mat_datagridView.RowCount - 2 )
            {
                bill_mat_datagridView.Rows[ current_row ].Selected = false;
                bill_mat_datagridView.Rows[ ++current_row ].Selected = true;
                bill_mat_datagridView.CurrentCell = bill_mat_datagridView.Rows[ current_row ].Cells[ bill_mat_datagridView.CurrentCell.ColumnIndex ];

                get_field_values_navigating_buttons( );
            }
        }

        private void move_prev_toolStripButton_Click ( object sender , EventArgs e ) {
            prev_record( );
        }
        public void prev_record ( ) {
            if ( current_row > 0 )
            {
                bill_mat_datagridView.Rows[ current_row ].Selected = false;
                bill_mat_datagridView.Rows[ --current_row ].Selected = true;
                bill_mat_datagridView.CurrentCell = bill_mat_datagridView.Rows[ current_row ].Cells[ bill_mat_datagridView.CurrentCell.ColumnIndex ];

                get_field_values_navigating_buttons( );
            }
        }

        private void prev_page_toolStripButton_Click ( object sender, EventArgs e ) {
            prev_page( );
        }
        public void prev_page ( ) {
            if ( start >= 30 && ds.Tables[ 0 ].Rows.Count == 30 )
            { /*Check condition if globalize int start initialized value of zero, is greater than or equal to 30 and Dataset ds row count is equal to 30*/
                start = start - 30; //if true minus 30 int start  until lblPageCount.text turns to 1 equivalent to first page or first 30 records
                if ( page_number_toolStripTextBox.Text != "1" )
                {  //check if text in lblPageCount is not equal to 1
                    pageCount = Convert.ToInt32( page_number_toolStripTextBox.Text ) - 1; //if true int pagecount initialized w/ value of 1, convert lblPageCount.text to int value then subract it with value of 1
                    page_number_toolStripTextBox.Text = pageCount.ToString( ); // then convert pagecount to a string value and place it to lblPageCount.Text
                }
                current_row = 0;
            }
            else
            {
                if ( page_number_toolStripTextBox.Text != "1" )
                {
                    pageCount = Convert.ToInt32( page_number_toolStripTextBox.Text ) - 1;
                    page_number_toolStripTextBox.Text = pageCount.ToString( );
                }
                if ( start != 0 )
                {
                    start = start - 30;
                }
                current_row = 0;
            }
            ds.Clear( );
            adapter.Fill( ds, Convert.ToInt32( start ), 30, "bill_mat" );
            current_record_count_toolStripLabel.Text = "Showing " + bill_mat_datagridView.RowCount.ToString( ) + " records of page : ";
        }

        private void bill_mat_datagridView_KeyDown ( object sender, KeyEventArgs e ) {
            if ( e.KeyCode==Keys.Left )
            {
                prev_page( );
            }
             if ( e.KeyCode==Keys.Right )
            {
           
            }
             if ( e.KeyCode==Keys.Up )
            {               
                if ( current_row > 0 )
                {
                    bill_mat_datagridView.Rows[ current_row ].Selected = false;
                    bill_mat_datagridView.Rows[ bill_mat_datagridView.CurrentRow.Index ].Selected = true;

                    bill_mat_datagridView.CurrentCell = bill_mat_datagridView.Rows[ bill_mat_datagridView.CurrentRow.Index ].Cells[ bill_mat_datagridView.CurrentCell.ColumnIndex ];

                    current_row--;
                    get_field_values_up_arrow( );                    
                }
            }
             if ( e.KeyCode==Keys.Down )
            {
                if ( current_row <= bill_mat_datagridView.RowCount - 2 )
                {
                    bill_mat_datagridView.Rows[ current_row ].Selected = false;
                    bill_mat_datagridView.Rows[ bill_mat_datagridView.CurrentRow.Index ].Selected = true;
                    
                    bill_mat_datagridView.CurrentCell = bill_mat_datagridView.Rows[ bill_mat_datagridView.CurrentRow.Index ].Cells[ bill_mat_datagridView.CurrentCell.ColumnIndex ];
                    current_row++;
                    get_field_values_down_arrow( );
                }
            }
        }
     
        private void print_toolStripButton_Click_1 ( object sender , EventArgs e ) {
            // tabControl1.Appearance = TabAppearance.Buttons;
            //tabControl1.SizeMode = TabSizeMode.Fixed;
            //tabControl1.ItemSize = new Size( 0 , 1 );
            //tabControl1.Alignment = TabAlignment.Top;
            Project_Price_Form.Project_price_Form fm = new Project_Price_Form.Project_price_Form( );
            fm.ShowDialog( );
            print_bill_mat = true;
            Reports.Dashboard.Dashboard_form form = new Reports.Dashboard.Dashboard_form( );
            form.ShowDialog( );
        }

        private void BillMat_Form_Load ( object sender , EventArgs e ) {            
         
        }

        private void project_toolStripDropDownButton_Click ( object sender , EventArgs e ) {
            New_project_form New = new New_project_form( );
            New.ShowDialog( );
        }
        Home_Form Home_form;
        private void back_toolStripButton_Click ( object sender , EventArgs e ) {
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

        private void Home_form_FormClosed ( object sender , FormClosedEventArgs e ) {
            Home_form = null;
        }

        private void timer_Fadeout_Tick ( object sender , EventArgs e ) {
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

        private void sub_scope_description_comboBox_Leave ( object sender , EventArgs e ) {
            try
            {
                bool enable = false;
                Data_fields_unfocus_setup( );
                Reusable_codes.Autocomplete_source auto = new Reusable_codes.Autocomplete_source( );

                foreach( var item in auto.Autocomplete_sub_scope_selection_get_items( sub_scope_description_comboBox , division_num_comboBox , main_scope_description_comboBox ) )
                {
                    if( sub_scope_description_comboBox.Text == item )
                    {
                        enable = true;
                        selection_automation g = new selection_automation( );
                        g.wbs_code_selection( division_num_comboBox , wbs_Code_comboBox , sub_scope_description_comboBox , main_scope_description_comboBox );
                    }
                }
                if( enable == false )
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

        private void sub_scope_description_comboBox_SelectionChangeCommitted ( object sender , EventArgs e ) {
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

        private void close_pictureBox_Click ( object sender, EventArgs e ) {
            Environment.Exit( Environment.ExitCode );
        }

        private void minimize_pictureBox_Click ( object sender, EventArgs e ) {
            WindowState = FormWindowState.Minimized;
        }

        private void project_toolStripDropDownButton_Click_1 ( object sender , EventArgs e ) {
         
        }
        Backup_db back = new Backup_db( );
        private void records_backuptoolStripButton_Click ( object sender , EventArgs e ) {
            back.export( Fields_reusable_base_class.bill_mat_table , Name );
            #region old codes
            //Backup_database.Export ex = new Backup_database.Export( );
            //ex.backup_records( "bill_mat" );
            #endregion
        }

        private void search_metroTextBox_KeyDown ( object sender, KeyEventArgs e ) {
            if ( e.KeyCode == Keys.Enter )
            {
                search_records( );
            }
        }

        private void search_metroTextBox_ButtonClick ( object sender, EventArgs e ) {
            search_records( );
        }

        private void backgroundWorker1_DoWork ( object sender , DoWorkEventArgs e ) {
            //search_records( );
        }

        private void BillMat_Form_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode==Keys.F1 )
            {
                back_toolStripButton_Click( this , new EventArgs( ) );
            }
        }

        private void import_ToolStripMenuItem_Click ( object sender , EventArgs e ) {
         
        }

        private void BillMat_Form_Deactivate( object sender , EventArgs e ) {
            Form_opacity_fade.form_opacity_fade( this );
        }

        private void BillMat_toolStrip_ItemClicked( object sender , ToolStripItemClickedEventArgs e ) {

        }

        private void vat_exclusive_metroTextBox_Leave( object sender , EventArgs e ) {
            Data_fields_unfocus_setup( );
        }

        private void vat_exclusive_metroTextBox_TextChanged( object sender , EventArgs e ) {
            if( vat_exclusive_metroTextBox.Text!= "VAT exclusive" )
            {
                vat_exclusive_metroTextBox.ForeColor = Color.Black;
            }
            else
            {
                vat_exclusive_metroTextBox.ForeColor = Color.DarkGray;
            }
        }

        private void vat_metroTextBox_TextChanged( object sender , EventArgs e ) {
            if( vat_metroTextBox.Text != "VAT" )
            {
                vat_metroTextBox.ForeColor = Color.Black;
            }
            else
            {
                vat_metroTextBox.ForeColor = Color.DarkGray;
            }
        }

        private void addToolStripMenuItem_Click( object sender , EventArgs e ) {
            New_project_form New = new New_project_form( );
            New.Show( );
        }

        private void selectToolStripMenuItem_Click( object sender , EventArgs e ) {
            Project_Selection.Project_selection_form select = new Project_Selection.Project_selection_form( );
            select.ShowDialog( );
        }

        #region resizing borderless form
        int mpx, mpy, w, h;
        bool mouse_press = false;
        private void BillMat_Form_MouseDown( object sender , MouseEventArgs e ) {
            if( e.Button == MouseButtons.Left  )
            {
                if( cursor_resize_x==true )
                {
                    Cursor.Current = Cursors.SizeWE;
                    mouse_press = true;
                    mpx = Cursor.Position.X;
                    mpy = Cursor.Position.Y;
                    w = Width;
                    h = Height;
                }
                else if( cursor_resize_y ==true )
                {
                    Cursor.Current = Cursors.SizeNS;
                    mouse_press = true;
                    mpx = Cursor.Position.X;
                    mpy = Cursor.Position.Y;
                    w = Width;
                    h = Height;
                }                        
            }
        }

        private void BillMat_Form_MouseUp( object sender , MouseEventArgs e ) {
            mouse_press = false;
        }

    

        bool cursor_resize_x = false;
        bool cursor_resize_y = false;
        private void BillMat_Form_MouseMove( object sender , MouseEventArgs e ) {
            int loc_x = Location.X + Width - 5;
            int loc_y = Location.Y + Height - 5;
         

            if(  Cursor.Position.X >= loc_x )
            {
                Cursor.Current = Cursors.SizeWE;
                cursor_resize_x = true;
            }
            else if( Cursor.Position.Y >= loc_y )
            {
                Cursor.Current = Cursors.SizeNS;
                cursor_resize_y = true;
            }
            else
            {
                cursor_resize_x = false;
                cursor_resize_y = false;
            }

            if( mouse_press == true && Cursor.Current == Cursors.SizeWE )
            {
                int form_width_difference = loc_x - Cursor.Position.X;
                int form_width_size = Width - form_width_difference;
                Width = form_width_size;
                exclamation_mark_pictureBox.Anchor = AnchorStyles.Top;              
            }
            else if( mouse_press == true && Cursor.Current == Cursors.SizeNS )
            {
                int form_height_difference = loc_y - Cursor.Position.Y;
                int form_height_size = Height - form_height_difference;
                Height = form_height_size;
                exclamation_mark_pictureBox.Anchor = AnchorStyles.Bottom;
            }
        }
        #endregion
    }
}
