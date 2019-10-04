using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using MySql.Data.MySqlClient;
using System.Windows.Forms;
using GSG_Builders.RevisionedUI.Warehouse_inventory;

namespace GSG_Builders.RevisionedUI.Reusable_codes {
    class datagridview_configuration : Fields_reusable_base_class  {

        //MySqlCommand cmd;

        //DataGridView bill_mat_datagridView;

        public datagridview_configuration() {

        }
        public datagridview_configuration(DataGridView _datagridView) {
            datagridview = _datagridView;
        }

        public void checkboxColumn() {
            DataGridViewCheckBoxColumn checkbox = new DataGridViewCheckBoxColumn();
            checkbox.Name = "checkbox";          
            datagridview.Columns.Add(checkbox);
            datagridview.Columns[0].Width = 23;
            datagridview.Columns[ "checkbox" ].HeaderText = "";
        }

        public void bill_mat_datagridView_configuration() {
            datagridview.Columns["division_num"].HeaderText = "Division no.";
            datagridview.Columns["wbs_code"].HeaderText = "WBS code";
            datagridview.Columns["material_labor"].HeaderText = "Item category";
            datagridview.Columns["main_scope_description"].HeaderText = "Main scope of work";
            datagridview.Columns["sub_scope_description"].HeaderText = "Sub-scope of work";
            datagridview.Columns["item_description"].HeaderText = "Item description";
            datagridview.Columns["unit"].HeaderText = "Unit measurement";
            datagridview.Columns["quantity"].HeaderText = "Quantity";
            datagridview.Columns["unit_cost"].HeaderText = "Unit cost";
            datagridview.Columns[ "remarks" ].HeaderText = "Remarks";
            datagridview.Columns[ "vat_exclusive" ].HeaderText = "VAT exclusive";
            datagridview.Columns["vat"].HeaderText = "VAT";
            datagridview.Columns["remarks"].HeaderText = "Remarks";
            datagridview.Columns["date_purchased"].HeaderText = "Date purchased";
            datagridview.Columns[ "total_amount" ].HeaderText = "Total amount";

            datagridview.Columns["bill_mat_id"].Visible = false;
            datagridview.Columns["main_project_id"].Visible = false;
            datagridview.Columns["sub_project_id"].Visible = false;
            datagridview.Columns["main_project"].Visible = false;
            datagridview.Columns["sub_project"].Visible = false;
            datagridview.Columns[ "date_for_sorting" ].Visible = false;

            datagridview.Columns[ 0 ].ReadOnly = false;
            datagridview.Columns[ 0 ].Frozen = true;

            for ( int i = 1 ; i < datagridview.Columns.Count ; i++ )
            {
                datagridview.Columns[ i ].ReadOnly = true;
            }
        }
        public void journal_datagridView_configuration ( ) {
            datagridview.Columns[ "division_num" ].HeaderText = "Division no.";
            datagridview.Columns[ "wbs_code" ].HeaderText = "WBS code";
            datagridview.Columns[ "material_labor" ].HeaderText = "Item category";
            datagridview.Columns[ "main_scope_description" ].HeaderText = "Main scope of work";
            datagridview.Columns[ "sub_scope_description" ].HeaderText = "Sub-scope of work";
            datagridview.Columns[ "item_description" ].HeaderText = "Item description";
            datagridview.Columns[ "source_dealer" ].HeaderText = "Source/dealer";
            datagridview.Columns[ "invoice_sales_number" ].HeaderText = "Invoice/sales no.";
            datagridview.Columns[ "brand" ].HeaderText = "Brand";
            datagridview.Columns[ "serial_number" ].HeaderText = "Serial no.";
            datagridview.Columns[ "unit" ].HeaderText = "Unit measurement";
            datagridview.Columns[ "quantity" ].HeaderText = "Quantity";
            datagridview.Columns[ "unit_cost" ].HeaderText = "Unit cost";
            datagridview.Columns[ "total_amount" ].HeaderText = "Total amount";
            datagridview.Columns[ "remarks" ].HeaderText = "Remarks";
            datagridview.Columns[ "vat_exclusive" ].HeaderText = "VAT exclusive";
            datagridview.Columns[ "vat" ].HeaderText = "VAT";
            datagridview.Columns[ "date_purchased" ].HeaderText = "Date purchased";

            datagridview.Columns[ "journal_id" ].Visible = false;
            datagridview.Columns[ "main_project_id" ].Visible = false;
            datagridview.Columns[ "sub_project_id" ].Visible = false;
            datagridview.Columns[ "main_project" ].Visible = false;
            datagridview.Columns[ "sub_project" ].Visible = false;
            datagridview.Columns[ "date_for_sorting" ].Visible = false;

            datagridview.Columns[ 0 ].ReadOnly = false;
            datagridview.Columns[ 0 ].Frozen = true;

            for ( int i = 1 ; i < datagridview.Columns.Count ; i++ )
            {
                datagridview.Columns[ i ].ReadOnly = true;
            }
        }
        public void inventory_datagridView_configuration( ) {
            datagridview.Columns[ "description" ].HeaderText = "Item description";
            datagridview.Columns[ "source_dealer" ].HeaderText = "Source/dealer";
            datagridview.Columns[ "date_purchased" ].HeaderText = "Date of purchased";
            datagridview.Columns[ "invoice_number" ].HeaderText = "Invoice-number";
            datagridview.Columns[ "quantity" ].HeaderText = "Quantity";
            datagridview.Columns[ "serial_number" ].HeaderText = "Serial-number";
            datagridview.Columns[ "unit_cost" ].HeaderText = "Unit cost";
            datagridview.Columns[ "amount" ].HeaderText = "Amount";
            datagridview.Columns[ "remarks" ].HeaderText = "Remarks";
            datagridview.Columns[ "unit" ].HeaderText = "Unit";
            datagridview.Columns[ "brand" ].HeaderText = "Brand";
            datagridview.Columns[ "category" ].HeaderText = "Category";

            datagridview.Columns[ "equipment_main_record_id" ].Visible = false;
            datagridview.Columns[ "status" ].Visible = false;          
            datagridview.Columns[ "picture" ].Visible = false;
            datagridview.Columns[ "date_for_sorting" ].Visible = false;            
           
            datagridview.Columns[ 0 ].Frozen = true;
            datagridview.Columns[ "description" ].Frozen = true;
            
        }
        public void assigned_project_datagridView( ) {
        
         
        }
        public void item_history_dataGridView( ) {
            datagridview.Columns[ "assignment_id" ].Visible = false;
            datagridview.Columns[ "equipment_main_record_id" ].Visible = false;
            datagridview.Columns[ "main_project_id" ].Visible = false;
            datagridview.Columns[ "description" ].Visible = false;
            datagridview.Columns[ "assignment_id" ].Visible = false;
            datagridview.Columns[ "date_for_sorting" ].Visible = false;
            datagridview.Columns[ "date_out" ].HeaderText = "Date transferred";
            datagridview.Columns[ "project_from" ].HeaderText = "From";           
            datagridview.Columns[ "project_assigned" ].HeaderText = "Assigned-in";
            datagridview.Columns[ "qty_out" ].HeaderText = "Quantity transferred";
            datagridview.Columns[ "amount" ].HeaderText = "Amount";
            datagridview.Columns[ "remarks" ].HeaderText = "Remarks";                     
        }
        public void hr_biodata_datagridView( ) {
            datagridview.Columns["worker_biodata_id"].Visible = false;
            datagridview.Columns[ "main_project_id" ].Visible = false;
            datagridview.Columns[ "worker_picture" ].Visible = false;
            datagridview.Columns[ "date_for_sorting" ].Visible = false;            
            datagridview.Columns[ 0 ].ReadOnly = false;

            string[ ] datagridview_header_text = new string[ ] {"First-name","Middle-name", "Surname" , "Assigned project", "Work status" , "Designation" , "Rate" , "Id-number" , "Date hired" , "Birthdate" , "Marital-status" , "Wife-name" , "Religion" , "Educational Attainment" , "Present-address" , "Provincial-address" , "Reference" , "Contact-number"  };
            int arr_index_num = 0;           

            foreach( DataGridViewColumn item in datagridview.Columns )
            {
                if( item.Visible && item.Name!= "checkbox" )
                {
                    datagridview.Columns[ item.Name ].HeaderText = datagridview_header_text[ arr_index_num ];
                    arr_index_num++;
                }
            }            
        }
        public void case_dataGridView( ) {
            datagridview.Columns[ "worker_biodata_id" ].Visible = false;
            datagridview.Columns[ "worker_case_id" ].Visible = false;
            datagridview.Columns[ "date_for_sorting" ].Visible = false;

            datagridview.Columns[ "worker_case" ].HeaderText = "Worker's case";
            datagridview.Columns[ "project" ].HeaderText = "Project";
            datagridview.Columns[ "date_of_case" ].HeaderText = "Date of case";
            datagridview.Columns[ "remarks" ].HeaderText = "Remarks";
          
        }

        public void designation_rate_dataGridView( ) {
            datagridview.Columns[ "worker_designation_rate_id" ].Visible = false;
            datagridview.Columns[ "worker_biodata_id" ].Visible = false;
            datagridview.Columns[ "date_for_sorting" ].Visible = false;

            datagridview.Columns[ "previous_designation" ].HeaderText = "Previous Designation";
            datagridview.Columns[ "previous_rate" ].HeaderText = "Previous rate";
            datagridview.Columns[ "category" ].HeaderText = "Category";
            datagridview.Columns[ "new_designation" ].HeaderText = "New Designation";
            datagridview.Columns[ "new_rate" ].HeaderText = "New rate";
            datagridview.Columns[ "date" ].HeaderText = "Date of changed";            
        }
        public void assigned_projects_dataGridView( ) {
            datagridview.Columns[ "worker_assignment_history_id" ].Visible = false;
            datagridview.Columns[ "worker_biodata_id" ].Visible = false;
            datagridview.Columns[ "first_name" ].Visible = false;
            datagridview.Columns[ "middle_name" ].Visible = false;
            datagridview.Columns[ "surname" ].Visible = false;
            datagridview.Columns[ "id_number" ].Visible = false;
            datagridview.Columns[ "designation" ].Visible = false;
            datagridview.Columns[ "date_for_sorting" ].Visible = false;

            datagridview.Columns[ "project_from" ].HeaderText = "Project from";
            datagridview.Columns[ "assigned_project" ].HeaderText = "Assigned project";
            datagridview.Columns[ "date_assigned" ].HeaderText = "Date assigned";
           
        }
    }
}
