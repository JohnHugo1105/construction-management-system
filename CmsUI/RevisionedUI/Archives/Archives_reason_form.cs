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

namespace GSG_Builders.RevisionedUI.Archives {
    public partial class Archives_reason_form : MetroFramework.Forms.MetroForm {

        public static string main_project_selected;
        public static string sub_project_selected;
        public static string date_started;
        public string reason_result;
                
        public Archives_reason_form ( ) {
            InitializeComponent( );
        }

     
     
        private void other_reason_richTextBox_KeyDown ( object sender, KeyEventArgs e ) {
           
        
        }

        private void others_specify_reason_radioButton_CheckedChanged ( object sender, EventArgs e ) {
            if ( others_specify_reason_radioButton.Checked )
            {
                other_reason_richTextBox.Enabled = true;
            }
            else
            {
                other_reason_richTextBox.Clear( );
                other_reason_richTextBox.Enabled = false;
            }
        }

        private void backgroundWorker1_DoWork ( object sender, DoWorkEventArgs e ) {

            Archiving ar = new Archiving( );

            if( Bill_Mat.ProjectConfigForm.main_project_selected_clicked == true )
            {
                ar.bill_mat_main_project_move_to_archives_main( main_project_selected );
                ar.main_sub_project_move_to_archives( main_project_selected , date_started , reason_result );
            }
            else if( Bill_Mat.ProjectConfigForm.sub_project_selected_clicked == true )
            {
                ar.bill_mat_sub_project_move_to_archives_main( main_project_selected , sub_project_selected );
                ar.sub_project_move_to_archives( main_project_selected , date_started , reason_result );
            }
        }

        private void okay_bunifuThinButton_Click ( object sender, EventArgs e ) {
         
        }

        private void cancel_bunifuThinButton_Click ( object sender, EventArgs e ) {
            Close( );
        }

        private void backgroundWorker1_ProgressChanged ( object sender , ProgressChangedEventArgs e ) {
            
        }

        private void backgroundWorker1_RunWorkerCompleted ( object sender , RunWorkerCompletedEventArgs e ) {

        }

        private void Archives_reason_form_Deactivate( object sender , EventArgs e ) {
            Form_opacity_fade.form_opacity_fade( this );
        }

        private void Archives_reason_form_Activated( object sender , EventArgs e ) {
            Opacity = 1.0;
        }

        private void Archives_reason_form_Load( object sender , EventArgs e ) {

        }
    }
}
