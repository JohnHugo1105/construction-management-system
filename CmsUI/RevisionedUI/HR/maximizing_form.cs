using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.HR {
    public partial class maximizing_form : MetroFramework.Forms.MetroForm {

        public static bool size_set = false;

        public maximizing_form( ) {
            InitializeComponent( );
            maximize_value_bunifuSlider.Value = Convert.ToInt32(HR_form.percentage_size);
            value_bunifuMetroTextbox.Text = Convert.ToInt32(HR_form.percentage_size).ToString();
        }

        private void maximizing_form_Load( object sender , EventArgs e ) {

        }

        private void label1_Click( object sender , EventArgs e ) {

        }

        private void maximize_value_bunifuSlider_Scroll( object sender , ScrollEventArgs e ) {
            
        }

        private void maximize_value_bunifuSlider_ValueChanged( object sender , EventArgs e ) {
            float value = maximize_value_bunifuSlider.Value;
            value_bunifuMetroTextbox.Text = value.ToString();
        }

        private void set_bunifuThinButton_Click( object sender , EventArgs e ) {
            HR_form.percentage_size = maximize_value_bunifuSlider.Value;
            size_set = true;
            Close( );
        }

        private void value_bunifuMetroTextbox_KeyDown( object sender , KeyEventArgs e ) {
            if( e.KeyCode==Keys.Enter )
            {
                maximize_value_bunifuSlider.Value = Convert.ToInt32( value_bunifuMetroTextbox.Text);               
            }
        }
    }
}
