using GSG_Builders.RevisionedUI.Reusable_codes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.Warehouse_inventory {
    public partial class Outgoing_form : MetroFramework.Forms.MetroForm {

        bool assigned_project_datagridView_clicked = false;

        public Outgoing_form( ) {
            InitializeComponent( );
            //item_description_textBox.Text = inventory_form.item_description;
            Autocomplete_source source = new Autocomplete_source( );
            source.Autocomplete_main_project( project_textBox );            
            load_records( );        
        }
        private void load_records( ) {
            //load_records load = new load_records( assigned_project_datagridView );
            //load.warehouse_inventory_outgoing( );

            item_inventory_count count = new item_inventory_count( );

            datagridview_configuration config = new datagridview_configuration( assigned_project_datagridView );
            config.assigned_project_datagridView( );
            
            //if( inventory_form.category != "tool" )
            //{
            //    quantity_textBox.Text = "1";
            //    quantity_textBox.ReadOnly = true;
            //}          
         
            if( form_load_count>0 )
            {
                Outgoing_form_Load( this , new EventArgs( ) );
            }

            project_list_selected_label.Text = "click for selection";
            assigned_project_datagridView_clicked = false;
        }

        Methods_return_type methods = new Methods_return_type( );
        private string project_from( ) {
            item_inventory_count item = new item_inventory_count( );

            string project_from = "";

            return project_from;
        }

        string quantity_string_field = "";
        private string quantity_string( ) {
            if( quantity_string_field==string.Empty )
            {
                quantity_string_field = quantity_textBox.Text;
            }
            return Convert.ToDouble(quantity_string_field).ToString( "#,##0.00" ); ;
        }

        private void label4_Click( object sender , EventArgs e ) {
           
        }       

        private void out_bunifuThinButton_Click( object sender , EventArgs e ) {
            Computation comp = new Computation( );

            item_inventory_count count = new item_inventory_count( );

            Record_count max_count = new Record_count( );

            Update_records up = new Update_records( );

            Methods_return_type count_tools = new Methods_return_type( );

       


        }

        private void cancel_bunifuThinButton_Click( object sender , EventArgs e ) {            
            this.Close( );
        }

        private void assigned_project_datagridView_CellClick( object sender , DataGridViewCellEventArgs e ) {
            if( assigned_project_datagridView_clicked == true )
            {
                project_list_selected_label.Text = "project unselected";
                assigned_project_datagridView_clicked = false;
            }
            else
            {
                bunifuTransition1.ShowSync( project_list_selected_label );
                project_list_selected_label.Text = "project selected";
                assigned_project_datagridView_clicked = true;
            }
        }
        int form_load_count = 0;
        private void Outgoing_form_Load( object sender , EventArgs e ) {
          
          
        }

        private void assigned_project_datagridView_Click( object sender , EventArgs e ) {
          
        }

        private void label5_Click( object sender , EventArgs e ) {
       
        }

        private void project_textBox_Leave( object sender , EventArgs e ) {
            if( project_textBox.Text=="Warehouse" )
            {
                without_amount_metroRadioButton.Checked = true;
            }
        }

        private void Outgoing_form_Activated( object sender , EventArgs e ) {
           
        }

        private void Outgoing_form_Deactivate( object sender , EventArgs e ) {
            
        }
    }
}
