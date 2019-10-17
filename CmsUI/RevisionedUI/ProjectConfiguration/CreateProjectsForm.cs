using CmsLibrary;
using CmsLibrary.ProjectConfiguration.Model;
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
            ProjectsSelectionCreateModel credential = new ProjectsSelectionCreateModel( ProjectConfigForm.Selection(), mainProjectNameTxt.Enabled, subProjectNameTxt.Enabled );

            if(credential.Selection == "All" )
            {
                GlobalConfig.ProjectsSelectionProcess.AllProjectsSelected( credential );
            }
            else if( credential.Selection == "Main" )
            {
                GlobalConfig.ProjectsSelectionProcess.MainProjectsSelected( credential );
            }
            else
            {
                GlobalConfig.ProjectsSelectionProcess.SubProjectsSelected( credential );
            }

        }
    }
}
