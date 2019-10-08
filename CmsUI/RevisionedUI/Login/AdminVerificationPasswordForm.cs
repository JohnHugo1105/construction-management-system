using System;
using System.Windows.Forms;
using CmsLibrary;
using CmsLibrary.Model.Login;
using CmsLibrary.BusinessLogic.SpEvents;

namespace GSG_Builders.Login {
    public partial class AdminVerificationPasswordForm : Form {

          string username = "";
          string password = "";
          string accountType = "";
          string userAccessCode = "";

        public AdminVerificationPasswordForm() {
            InitializeComponent();
        }

        private void txtPassword_TextChanged(object sender, EventArgs e) {
            try
            {
                if (txtPassword.Text.Length >= 15)
                {
                    txtPassword.MaxLength = 15;
                    MessageBox.Show("Max character only of " + 15, "Max length reach", MessageBoxButtons.RetryCancel, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error! : " + ex.Message, "ERROR!", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {
            if (checkBox1.Checked == true)
            {
                txtPassword.PasswordChar = '\0';
            }
            else
            {
                txtPassword.PasswordChar = '▪';
            }
        }

        private void AdminVerificationPassword_Load(object sender, EventArgs e) {
             username = CreateAccountsForm.username;
             password = CreateAccountsForm.password;
            accountType = CreateAccountsForm.accountType;
            userAccessCode = CreateAccountsForm.userAccessCode;

        }

        private void txtPassword_KeyDown(object sender, KeyEventArgs e) {
         
                if( e.KeyCode ==Keys.Enter )
            {
                if( ValidateInput( ) == true )
                {
                    if( accountType != "User" )
                    {
                        CreateAdmin( );
                        MessageBox.Show( "Admin account successfuly created!","Create account",MessageBoxButtons.OK,MessageBoxIcon.Information );
                        Close( );
                    }
                    else
                    {
                        CreateUser( );
                        MessageBox.Show( "User account successfuly created!" , "Create account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                        Close( );
                    }
                }
                else
                {
                    MessageBox.Show( "Create account failed! Check your credentials" , "Login failed" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Error );
                }
            }     
        }

        /// <summary>
        /// Validates credentials of informations input
        /// </summary>
        /// <returns></returns>
        private bool ValidateInput( ) {
            ValidateInputModel valid = new ValidateInputModel(username,password,accountType,userAccessCode );
            return GlobalConfig.LoginValidation.IsValidInput(valid );
        }
        private void CreateAdmin( ) {
            AdminModel credentials = new AdminModel( username.Replace( "'" , "''" ) , password.Replace( "'" , "''" ) ,
                   accountType );
            GlobalConfig.LoginAdminConnection.Create( SpLoginEventsList.spAccountCreate , credentials , "AdminAccount" );
        }

        private void CreateUser( ) {
            UserModel credentials = new UserModel( username.Replace( "'" , "''" ) , password.Replace( "'" , "''" ) ,
                   accountType , userAccessCode );
            GlobalConfig.LoginUserConnection.Create( SpLoginEventsList.spAccountCreate , credentials , "UserAccount" );
        }

    }
}
