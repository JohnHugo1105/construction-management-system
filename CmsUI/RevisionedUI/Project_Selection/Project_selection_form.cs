using GSG_Builders.RevisionedUI.Bill_Mat;
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

namespace GSG_Builders.RevisionedUI.Project_Selection {
    public partial class Project_selection_form : Form {
        public Project_selection_form ( ) {
            InitializeComponent( );

            load_main_projects( );
            load_sub_projects( );

        }
        public void load_main_projects ( ) {
       
        }
        public void load_sub_projects ( ) {
       
        }
        private void bill_mat_settings_main_project_dataGridView_CellDoubleClick ( object sender , DataGridViewCellEventArgs e ) {
            if( Application.OpenForms.OfType<BillMat_Form>( ).Count( ) == 1 )
            {
                BillMat_Form.project_selected_str = main_project_dataGridView.CurrentRow.Cells[ "project_name" ].Value.ToString( );

                BillMat_Form.new_project_form_count = 1;
            }
            else if( Application.OpenForms.OfType<Journal.Journal_form>( ).Count( ) == 1 )
            {
                Journal.Journal_form.project_selected_str = main_project_dataGridView.CurrentRow.Cells[ "project_name" ].Value.ToString( );
                Journal.Journal_form.new_project_form_count = 1;
            }
            Close( );
        }

        private void main_project_dataGridView_CellClick ( object sender , DataGridViewCellEventArgs e ) {
           
        }

        private void panel2_Click ( object sender , EventArgs e ) {
          
        }

        private void close_pictureBox_Click ( object sender , EventArgs e ) {
            Close( );
        }

        private void Project_selection_form_Deactivate( object sender , EventArgs e ) {
            Form_opacity_fade.form_opacity_fade( this );
        }

        private void Project_selection_form_Activated( object sender , EventArgs e ) {
            Form_opacity_fade.form_opacity_fade_in( this );
        }
    }
}
