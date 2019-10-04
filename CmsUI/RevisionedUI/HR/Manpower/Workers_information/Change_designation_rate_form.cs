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
    public partial class Change_designation_rate_form : MetroFramework.Forms.MetroForm {
        public Change_designation_rate_form( ) {
            InitializeComponent( );
            current_designation_textBox.Text = HR_form.designation;
            current_rate_textBox.Text = HR_form.rate;
        }

        private void groupBox1_Enter( object sender , EventArgs e ) {

        }

        private void Change_designation_rate_form_Load( object sender , EventArgs e ) {

        }

        private string category( ) {
            string category = "";
            if( Promotion_radioButton.Checked )
            {
                category = Promotion_radioButton.Text;
            }
            else if( demotion_radioButton.Checked )
            {
                category = demotion_radioButton.Text;
            }
            else
            {
                category = rate_adjustment_radioButton.Text;
            }
            return category;
        }
        private void okay_bunifuThinButton_Click( object sender , EventArgs e ) {
      
        }

        private void cancel_bunifuThinButton_Click( object sender , EventArgs e ) {
            Close( );
        }
    }
}
