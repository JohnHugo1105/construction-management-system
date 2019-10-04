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
    public partial class History_form : MetroFramework.Forms.MetroForm {

        int current_row = 0;

        public History_form( ) {
            InitializeComponent( );
            load_records( );
        }

        private void load_records( ) {
         
        }

        private int cell_y_position( ) {
            int y = 0;
            return y = ( ( item_history_dataGridView.CurrentRow.Index + 7 ) * item_history_dataGridView.RowTemplate.Height - 5 );
        }

        private void History_form_Load( object sender , EventArgs e ) {
            int column_count = 0;
            
            foreach( DataGridViewColumn item in item_history_dataGridView.Columns )
            {
                if( item.Visible )
                {
                    column_count += item.Width;
                }
            }
            column_count = column_count + item_history_dataGridView.RowHeadersWidth;
            if( column_count < item_history_dataGridView.Width )
            {
                item_history_dataGridView.Columns[ "remarks" ].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }

        bool edit_mode = false;
        private void editItemToolStripMenuItem_Click( object sender , EventArgs e ) {
            edit_mode = true;
            Autocomplete_source source = new Autocomplete_source( );
            //if( item_history_dataGridView.CurrentCell.OwningColumn.Name != "checkbox" )
            //{
            if( item_history_dataGridView.CurrentCell.OwningColumn.Name == "date_out" )
            {
                dateTimePicker2.Location = new Point( item_history_dataGridView.GetCellDisplayRectangle( item_history_dataGridView.CurrentCell.ColumnIndex , row_index , false ).Location.X + item_history_dataGridView.CurrentCell.Size.Width - 20 , cell_y_position( ) );
                dateTimePicker2.Text = item_history_dataGridView.CurrentRow.Cells[ "date_out" ].Value.ToString( );
                dateTimePicker2.Visible = true;
            }
            else if( item_history_dataGridView.CurrentCell.OwningColumn.Name == "project_from" || item_history_dataGridView.CurrentCell.OwningColumn.Name == "project_assigned" )
            {
                project_from_assigned_textBox.Location = new Point( item_history_dataGridView.GetCellDisplayRectangle( item_history_dataGridView.CurrentCell.ColumnIndex , row_index , false ).Location.X + item_history_dataGridView.CurrentCell.Size.Width - 20 , cell_y_position( ) );
                // dateTimePicker2.Text = item_history_dataGridView.CurrentRow.Cells[ "date_out" ].Value.ToString( );
                project_from_assigned_textBox.Visible = true;
                source.Autocomplete_main_project( project_from_assigned_textBox );
            }
            else
            {
                item_history_dataGridView.ReadOnly = false;
                item_history_dataGridView.BeginEdit( true );
            }           
        }
        int row_index = 0;
        private void item_history_dataGridView_RowLeave( object sender , DataGridViewCellEventArgs e ) {
            row_index = e.RowIndex;
        }

        private void item_history_dataGridView_CellEndEdit( object sender , DataGridViewCellEventArgs e ) {
        
        }

        private void dateTimePicker2_CloseUp( object sender , EventArgs e ) {
         
        }

        private void item_history_dataGridView_CellMouseDown( object sender , DataGridViewCellMouseEventArgs e ) {
            if( e.Button == MouseButtons.Right )
            {
                item_history_dataGridView.CurrentCell = item_history_dataGridView.Rows[ e.RowIndex ].Cells[ e.ColumnIndex ];
                item_history_dataGridView.Rows[ e.RowIndex ].Selected = true;
                current_row = item_history_dataGridView.CurrentRow.Index;                
            }
        }

        private void item_history_dataGridView_CurrentCellDirtyStateChanged( object sender , EventArgs e ) {
            if( item_history_dataGridView.IsCurrentCellDirty )
            {
                item_history_dataGridView.CommitEdit( DataGridViewDataErrorContexts.Commit );
            }
        }

        private void dateTimePicker2_Leave( object sender , EventArgs e ) {
            dateTimePicker2.Visible = false;
        }

        bool on_hover = false;
      //  bool click_outside_datepicker = false;
        private void dateTimePicker2_MouseHover( object sender , EventArgs e ) {
            on_hover = true;
          
        }

        private void dateTimePicker2_MouseLeave( object sender , EventArgs e ) {
            on_hover = false;
            dateTimePicker2.Visible = false; 
        }

        private void dateTimePicker2_MouseDown( object sender , MouseEventArgs e ) {
           
        }

        private void removeToolStripMenuItem_Click( object sender , EventArgs e ) {
          
        }

        private void project_from_assigned_textBox_MouseLeave( object sender , EventArgs e ) {
            project_from_assigned_textBox.Visible = false;

            project_from_assigned_textBox.AutoCompleteCustomSource.Clear( );
        }

        private void project_from_assigned_textBox_KeyDown( object sender , KeyEventArgs e ) {
            //Methods_return_type ret = new Methods_return_type( );
            //Update_records rec = new Update_records( );

            ////int qty_count = Convert.ToInt32( item_history_dataGridView.CurrentRow.Cells[ "qty_out" ].Value.ToString( ) ) - ret.Warehouse_inventory_tool_qunatity_count( "project_assigned" , project_from_assigned_textBox.Text.Replace( "'" , "''" ) , "equipment_main_record_id" , item_history_dataGridView.CurrentRow.Cells[ "equipment_main_record_id" ].Value.ToString( ) );

            //if( e.KeyCode == Keys.Enter &&  project_from_assigned_textBox.Text!="Warehouse" && item_history_dataGridView.CurrentRow.Cells["project_from"].Value.ToString()=="Warehouse" || e.KeyCode == Keys.Enter && project_from_assigned_textBox.Text != "Warehouse" && item_history_dataGridView.CurrentRow.Cells[ "project_assigned" ].Value.ToString( ) == "Warehouse" )
            //{
            //    rec.warehouse_inventory_history_edit_projects( " construction_management_system.warehouse_inventory_tool_equipment_history_assignment " , item_history_dataGridView.CurrentCell.OwningColumn.Name , project_from_assigned_textBox.Text.Replace( "'" , "''" ) , "assignment_id" , item_history_dataGridView.CurrentRow.Cells[ "assignment_id" ].Value.ToString( ) );

            //    if( item_history_dataGridView.CurrentRow.Cells[ "project_from" ].Value.ToString( ) == "Warehouse" && ret.Warehouse_inventory_tool_count(  "project_assigned", project_from_assigned_textBox.Text.Replace( "'" , "''" ) , "equipment_main_record_id" ,item_history_dataGridView.CurrentRow.Cells[ "equipment_main_record_id" ].Value.ToString() )==0  )
            //    {
            //        //if( ret.Warehouse_inventory_tool_qunatity_count( "project_assigned" , project_from_assigned_textBox.Text.Replace( "'" , "''" ) , "equipment_main_record_id" , item_history_dataGridView.CurrentRow.Cells[ "equipment_main_record_id" ].Value.ToString( ) ) == 0)
            //        //{

            //        //}
            //        //rec.warehouse_inventory_history_edit_projects( " construction_management_system.warehouse_inventory_tool " , item_history_dataGridView.CurrentCell.OwningColumn.Name , project_from_assigned_textBox.Text.Replace( "'" , "''" ) , "assignment_id" , item_history_dataGridView.CurrentRow.Cells[ "assignment_id" ].Value.ToString( ) );
            //    }
            //  //  MessageBox.Show( qty_count.ToString() );
            //    //MessageBox.Show( ret.Warehouse_inventory_tool_qunatity_count( "project_assigned" , project_from_assigned_textBox.Text.Replace( "'" , "''" ) , "equipment_main_record_id" , item_history_dataGridView.CurrentRow.Cells[ "equipment_main_record_id" ].Value.ToString( ) ).ToString());
            //    //MessageBox.Show( "Project detail successfuly updated!" , "Edit project" , MessageBoxButtons.OK , MessageBoxIcon.Information );
            //    project_from_assigned_textBox.Clear( );
            //    //load_records( );
            //}
            //else
            //{

            //}
        }

        private void item_history_dataGridView_CellClick( object sender , DataGridViewCellEventArgs e ) {
          
        }

        private void History_form_Activated( object sender , EventArgs e ) {
           
        }

        private void History_form_Deactivate( object sender , EventArgs e ) {
            
        }
    }
}
