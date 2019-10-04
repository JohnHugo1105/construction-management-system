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

namespace GSG_Builders.RevisionedUI.HR.Manpower.Workers_information {
    public partial class multiple_assignment : MetroFramework.Forms.MetroForm {
        public multiple_assignment ( ) {
            InitializeComponent( );
            add_projects_to_comboBox( );

            from_project_comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            from_project_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            assign_project_comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            assign_project_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;

       
            checkbox_column( );
        }
        public void add_projects_to_comboBox( ) {
          
        }
        private void checkbox_column( ) {
            datagridview_configuration checkbox = new datagridview_configuration( multiple_datagridView );
            checkbox.checkboxColumn( );
        }
        private void cancel_bunifuThinButton_Click ( object sender, EventArgs e ) {
            Close( );
        }

        private void project_comboBox_SelectedIndexChanged( object sender , EventArgs e ) {
           
        }

        private void project_comboBox_SelectionChangeCommitted( object sender , EventArgs e ) {
           
        }

        private void label1_Click( object sender , EventArgs e ) {
          
        }

        private void from_project_comboBox_SelectedIndexChanged( object sender , EventArgs e ) {
         
        }

        private void okay_bunifuThinButton_Click( object sender , EventArgs e ) {
            Save_record save = new Save_record( );
            Update_records update = new Update_records( );
            Methods_return_type main_proj_id = new Methods_return_type( );

            foreach( var  item in multiple_datagridView.Rows.Cast<DataGridViewRow>().ToArray() )
            {
                bool checkbox = Convert.ToBoolean( item.Cells[ 0 ].Value );
                if( checkbox==true )
                {                    
                 
                }
            }

        
        }     

        private void multiple_datagridView_CellValueChanged( object sender , DataGridViewCellEventArgs e ) {
            foreach( DataGridViewRow item in multiple_datagridView.Rows )
            {
                bool check = Convert.ToBoolean( item.Cells[ 0 ].Value );
                if( check == true )
                {
                    item.Selected = true;
                }
            }
        }

        private void multiple_datagridView_CurrentCellDirtyStateChanged( object sender , EventArgs e ) {
            if( multiple_datagridView.IsCurrentCellDirty )
            {
                multiple_datagridView.CommitEdit( DataGridViewDataErrorContexts.Commit );
            }
        }

        private void multiple_assignment_Load( object sender , EventArgs e ) {

        }
    }
}
