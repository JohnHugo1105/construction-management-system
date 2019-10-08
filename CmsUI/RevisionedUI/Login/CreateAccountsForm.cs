using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using GSG_Builders.Login;
using System.Data.SqlClient;
using GSG_Builders.RevisionedUI.Reusable_codes;
using CmsLibrary.Models;
using CmsLibrary;
using CmsLibrary.DataAccess;
using System.Collections.Generic;
using System.Linq;
using CmsLibrary.BusinessLogic.SpEvents;

namespace GSG_Builders {
    public partial class CreateAccountsForm : Form
    {
       
        public CreateAccountsForm()
        {
            InitializeComponent();
        }

        int countCheck = 0;
        private void CheckBoxChecked( object sender , EventArgs e ) {
            CheckBox ch = ( CheckBox ) sender;
            
            if( ch.Checked )
            {
                countCheck = countCheck + 1; //if checkbox is to be checked increment by 1
                if( countCheck == 3 && accountTypeTextBox.Text == "User" )//if checkBox checked counts is 3 and account is user 
                {
                    DisableCheckBox( ); //Disable the 4th checkBox that is not yet checked
                    //PopupUserPrivilegeInfo( );//then show popup notification
                }
            }
            else if( ch.Checked == false )
            {
                countCheck = countCheck - 1;
                EnableCheckBox(  );
            }
        }

        public void EnableCheckBox(  ) {
            AccessCodesModel checkBox = new AccessCodesModel(costMonitoringCheckBox.Checked , accountingCheckBox.Checked , procurementCheckBox.Checked , humanResourceCheckBox.Checked,costMonitoringCheckBox,accountingCheckBox,procurementCheckBox,humanResourceCheckBox );
            GlobalConfig.LoginCodes.EnableCheckBox(checkBox );           
        }

        public void DisableCheckBox( ) {
            AccessCodesModel checkBox = new AccessCodesModel( costMonitoringCheckBox.Checked , accountingCheckBox.Checked , procurementCheckBox.Checked , humanResourceCheckBox.Checked , costMonitoringCheckBox , accountingCheckBox , procurementCheckBox , humanResourceCheckBox );
            GlobalConfig.LoginCodes.DisableCheckBox( checkBox );          
        }

        private void PasswordForm_Load(object sender, EventArgs e)
        {
   
        }
    
        private void TextPassword_KeyDown( object sender, KeyEventArgs e ) {
            if( e.KeyCode==Keys.Enter )
            {
                LoginToSystem( );
            }
        }    

        private void TextPassword_TextChanged(object sender, EventArgs e) {
       
        }
             
        private void PasswordForm_MouseDown(object sender, MouseEventArgs e) {
    
        }

        private void PasswordForm_MouseMove(object sender, MouseEventArgs e) {
       
        }

        private void PasswordForm_MouseUp(object sender, MouseEventArgs e) {
          
        }
        private void btnLogin_Click(object sender, EventArgs e) {
            LoginToSystem( );
        }
        public static string AccessCode = "";
        private void LoginToSystem( ) {
            if( AccountsLoginValidation( ) == true )// if account exist admin or user next is get the access privilege code
            {
               AccessCode = GetLoginAccessCode( );
                RevisionedUI.Home.Home_Form home = new RevisionedUI.Home.Home_Form( );
                home.Show( );
                Hide( );
            }
            else
            {
                MessageBox.Show( "Invalid/missing account credentials, check and try again!" , "Invalid" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Warning );
            }
        }
        private string GetLoginAccessCode( ) {
            string code = "";
            var table = GlobalConfig.LoginGlobalConnection.GetAllAccounts( SpLoginEventsList.spGetAllAccounts  );
            var rows = table.AsEnumerable( )
                              .Where( r => r.Field<string>( 1 ) == txtUsername.Text &&
                                                    r.Field<string>( 2 ) == txtPassword.Text );
            var dt = rows.Any( ) ? rows.CopyToDataTable( ) : table.Clone( );

            foreach( DataRow item in dt.Rows )
            {
                code = item[ "LoginAccessCode" ].ToString( );
            }
            return  code;
        }

        public static string username = "";
        public static string password = "";
        public static string accountType = "";
        public static string userAccessCode = "";
        private void btnCreateUserAccount_Click(object sender, EventArgs e) {
            username = txtUsername.Text;
            password = txtPassword.Text;
            accountType = accountTypeTextBox.Text;
            userAccessCode = UserAccessCode( );

            if( AdminUsernameExist()==false && UserUsernameExist()==false )
            {
                AdminVerificationPasswordForm verfiy = new AdminVerificationPasswordForm( );
                verfiy.ShowDialog( );
            }
            else
            {
                MessageBox.Show( "This username was already been used, try another one" , "Username exist" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Information );
            } 
              
        }

        private bool AdminUsernameExist( ) {
            AdminModel credentials = new AdminModel( txtUsername.Text.Replace( "'" , "''" ) );
            return GlobalConfig.LoginGlobalConnection.IsUsernameExist( SpLoginEventsList.spIsUsernameExist , credentials , "AdminAccount" );
        }
        private bool UserUsernameExist( ) {
            UserModel credentials = new UserModel( txtUsername.Text.Replace( "'" , "''" ) );
            return GlobalConfig.LoginGlobalConnection.IsUsernameExist( SpLoginEventsList.spIsUsernameExist , credentials , "UserAccount" );
        }

        private bool AccountsLoginValidation( ) {
            AdminModel credentials = new AdminModel( txtUsername.Text , txtPassword.Text );
           return  GlobalConfig.LoginGlobalConnection.IsCredentialsValid( SpLoginEventsList.spIsCredentialsValid , credentials );
        }

        private string UserAccessCode( ) {
            AccessCodesModel codes = new AccessCodesModel( costMonitoringCheckBox.Checked , accountingCheckBox.Checked , procurementCheckBox.Checked , humanResourceCheckBox.Checked );
            return GlobalConfig.LoginCodes.UserAccessCode( codes );
        }

        private void CheckBoxKeyDownCheck( object sender , KeyEventArgs e ) {
            CheckBox ch = ( CheckBox ) sender;

            if( e.KeyCode == Keys.Enter )
            {
                if( ch.Checked )
                {
                    ch.Checked = false;
                }
                else
                {
                    ch.Checked = true;
                }
            }
        }
     
        private void btnCancel_Click(object sender, EventArgs e) {
            Close( );
        }
      
        private void CreateAccountsForm_FormClosed( object sender , FormClosedEventArgs e ) {
      
        }

        private void close_pictureBox_Click_1( object sender , EventArgs e ) {
          
        }             

        private void pictureBox5_Click( object sender , EventArgs e ) {
            if( show_password_pictureBox.Visible == true )
            {
                show_password_pictureBox.Visible = false;
                txtPassword.PasswordChar = '\0';
                hide_password_pictureBox.Visible = true;
            }
        }

        private void pictureBox3_Click( object sender , EventArgs e ) {
            if( hide_password_pictureBox.Visible == true )
            {
                hide_password_pictureBox.Visible = false;
                txtPassword.PasswordChar = '▪';
                show_password_pictureBox.Visible = true;
            }
        }

        private void CreateAccountsForm_FormClosing( object sender , FormClosingEventArgs e ) {
           
        }

        private void admin_bunifuThinButton22_Click( object sender , EventArgs e ) {
           
            if( AdminUsernameExist()==true )
            {
                AdminControlForm acf = new AdminControlForm( );
                acf.Show( );
            }
        }

        private void CreateAccountsForm_Deactivate( object sender , EventArgs e ) {
        
        }

        private void CreateAccountsForm_Activated( object sender , EventArgs e ) {
         
        }

        private void accountTypeTextBox_TextChanged( object sender , EventArgs e ) {
            if( accountTypeTextBox.Text=="Admin" )
            {
                PrivilegeGroupBox.Enabled = false;
                accountingCheckBox.Checked = true;
                costMonitoringCheckBox.Checked = true;
                procurementCheckBox.Checked = true;
                humanResourceCheckBox.Checked = true;
            }
            else if(accountTypeTextBox.Text=="User")
            {
                PrivilegeGroupBox.Enabled = true;
                accountingCheckBox.Checked = false;
                costMonitoringCheckBox.Checked = false;
                procurementCheckBox.Checked = false;
                humanResourceCheckBox.Checked = false;
            }
        }

    }
}
