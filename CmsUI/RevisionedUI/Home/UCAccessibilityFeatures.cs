using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.AccessibilityFeaturesControl {
    public partial class UCAccessibilityFeatures : UserControl {
     //   string AdminLoggedIn = "ADMIN";
        public UCAccessibilityFeatures() {
            InitializeComponent();

            bfChckbShowPassword.BackColor = Color.DimGray;
            bfChckbCreateinput.BackColor = Color.DimGray;
            bunifuMaterialTextbox1.Text = "ADMIN";

            
        }

        private void bfChckbLogin_OnChange(object sender, EventArgs e) {
            Bunifu.Framework.UI.BunifuCheckbox bfchk = (Bunifu.Framework.UI.BunifuCheckbox)sender;
            Bunifu.Framework.UI.BunifuCheckbox[] bfchckArray = new Bunifu.Framework.UI.BunifuCheckbox[] { bfChckbLogin, bfChckbCreate, bfChckbAdmin };

            foreach (var item in bfchckArray)
            {
                if (item.Checked)
                {
                    if (bfchk.Name != item.Name)
                    {
                        item.Checked = false;
                        if (bfchk.Name == "bfChckbLogin")
                        {
                                     mtAccessControl.SelectedTab = mtTPlogin;
                        }
                        else if (bfchk.Name == "bfChckbCreate")
                        {
                                    mtAccessControl.SelectedTab = mtTPcreate;
                        }
                        else
                        {
                            mtAccessControl.SelectedTab = mtTPadmin;
                            //this.mtAccessControl.Selected += new System.Windows.Forms.TabControlEventHandler(this.mtAccessControl_Selected);
                        }
                    }
                }
            }
        }

        private void bfFlatBtnLogin_Click(object sender, EventArgs e) {
           
        }
        //RevisionedUI.Login.LoginCreateForm UAF = new Login.LoginCreateForm();
        private void UCAFbtnLogin_Click(object sender, EventArgs e) {
            //bunifuTransition1.HideSync(this);
            //bunifuTransition2.Show(UAF.uCjournal1);
        }

        private void mtAccessControl_Selected(object sender, TabControlEventArgs e) {
            //if (bfTxtUsername.text != "ADMIN" )
            //{
            //    mtAccessControl.SelectedTab =  mtTPlogin;
                
            //}
            //else
            //{
            //    mtAccessControl.SelectedTab = mtTPadmin;
               
            //}
            
        }
    }
}
