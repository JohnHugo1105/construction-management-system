using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.Project_Price_Form {
    public partial class Project_price_Form : MetroFramework.Forms.MetroForm {
        public Project_price_Form( ) {
            InitializeComponent( );
        }

        private void Project_price_Form_Load( object sender , EventArgs e ) {

        }

        private void textBox1_KeyDown( object sender , KeyEventArgs e ) {
            if( e.KeyCode==Keys.Enter )
            {
                Reports.Dashboard.Dashboard_form.Project_price = textBox1.Text;
                Close( );
            }
        }
    }
}
