using CmsLibrary;
using GSG_Builders.RevisionedUI.Bill_Mat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.ProjectConfiguration {
    public partial class CreateProjectsForm : MetroFramework.Forms.MetroForm {
        public CreateProjectsForm( ) {
            InitializeComponent( );
        }

        private void CreateProjectsForm_Load( object sender , EventArgs e ) {
            if( ProjectConfigForm.allProjectsSelected==true )
            {
                mainProjectNameTxt.Enabled = true;
                subProjectNameTxt.Enabled = true;
            }
            else if( ProjectConfigForm.mainProjectsSelected==true )
            {
                mainProjectNameTxt.Enabled = true;
                subProjectNameTxt.Enabled = false;
            }
            else
            {
                mainProjectNameTxt.Enabled = false;
                subProjectNameTxt.Enabled = true;
            }

        }
    }
}
