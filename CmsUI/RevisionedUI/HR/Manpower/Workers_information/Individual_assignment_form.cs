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
    public partial class Individual_assignment_form : MetroFramework.Forms.MetroForm {
        public Individual_assignment_form ( ) {
            InitializeComponent( );


            add_projects_to_comboBox( );

            assigned_project_comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            assigned_project_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;

        
        }
        public void add_projects_to_comboBox( ) {
     
        }

        private void cancel_bunifuThinButton_Click ( object sender , EventArgs e ) {
            Close( );
        }

        private void okay_bunifuThinButton_Click( object sender , EventArgs e ) {
        
       
        }

        private void Individual_assignment_form_Load( object sender , EventArgs e ) {

        }
    }
}
