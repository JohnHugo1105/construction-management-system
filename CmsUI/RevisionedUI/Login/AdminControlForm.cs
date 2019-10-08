//using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
//using GSG_Builders.PasswordClasses;
using System.Data.SqlClient;
using GSG_Builders.RevisionedUI.Reusable_codes;
using System.Linq;
using System.Collections.Generic;
using CmsLibrary;
using CmsLibrary.Models;
using CmsLibrary.BusinessLogic.SpEvents;
//using CMS_Class.Class.Login.Implementations;
//using BllLibrary.Login;
//using PropertiesLibrary.Login.StaticProperties;
//using BllLibrary.Class.Login.Implementations;
//using PropertiesLibrary.Login.StaticProperties;
//using CMS_Class.Class.Login.Implementations;
//using BllLibrary.Login;

namespace GSG_Builders.Login {
    public partial class AdminControlForm : Form {
    

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn( // this is declaring the properties to make the form rounded corner
            int LeftEdge,
            int TopEdge,
            int Width,
            int Height,
            int TopEdgeCurvature,
            int BottomEdgeCurvature
            );

        public AdminControlForm() {
            InitializeComponent();
            LoadAccounts();

            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 25, 25)); // setting the rounded corners of the form
        }

        #region Dragging function form borderless
        /* this is to set a drag function to a form without a formborder */
        private bool mouseDown;
        private Point lastLocation;

        private void AdminControlForm_MouseDown(object sender, MouseEventArgs e) {
            //mouseDown = true;
            //lastLocation = e.Location;
        }

        private void AdminControlForm_MouseMove(object sender, MouseEventArgs e) {
            //if (mouseDown)
            //{
            //    this.Location = new Point(
            //        (this.Location.X - lastLocation.X) + e.X, (this.Location.Y - lastLocation.Y) + e.Y);

            //    this.Update(); 
            //}
        
        }
        
        private void AdminControlForm_MouseUp(object sender, MouseEventArgs e) {
            //mouseDown = false;
        }
        /*ENDS HERE*/
        #endregion

        private DataTable GetAdminAccounts( ) {
           return GlobalConfig.LoginGlobalConnection.GetAllAccounts(GlobalConfig.LoginSpEvents.spGetAllAccounts );
        }
        private void LoadAccounts() { // loading the user accounts of datagridview in admin control form
            dgvAccounts.DataSource = GetAdminAccounts( );
            dgvAccounts.Columns[ "id" ].Visible = false;
            dgvAccounts.Columns[ "Password" ].Visible = false;
            dgvAccounts.Columns[ "LoginAccessCode" ].Visible = false;
            dgvAccounts.Columns[ "Type" ].Visible = false;
            dgvAccounts.Columns[ "Username" ].Width = dgvAccounts.Width;
            dgvAccounts.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dgvAccounts.Update( );

            AdminCountLbl.Text = AdminCount( );
            UserCountLbl.Text = UserCount( );
        }
        private string AdminCount( ) {
          return GlobalConfig.LoginAdminConnection.AdminGetAccounts( SpLoginEventsList.spAdminGetAccounts ).Rows.Count.ToString( );//Sets the number of Admin accounts            
        }
        private string UserCount( ) {
            return  GlobalConfig.LoginUserConnection.UserGetAccounts( SpLoginEventsList.spUserGetAccounts ).Rows.Count.ToString( );//Sets the number of User accounts
        }
       
        private void AccountListForeColor() { //setting the font colors for admins-red and for users-green in the datagridview list of accounts
            foreach( DataGridViewRow item in dgvAccounts.Rows )
            {
                if( item.Cells[ "Type" ].Value.ToString( ) == "User" )
                {
                    int count = item.Index;

                    dgvAccounts.Rows[ count ].DefaultCellStyle.ForeColor = Color.Red;

                }
                else if( item.Cells[ "Type" ].Value.ToString( ) == "Admin" )
                {
                    int count = item.Index;
                    dgvAccounts.Rows[ count ].DefaultCellStyle.ForeColor = Color.DodgerBlue;
                }
            }
        }
        
        private void AdminControlForm_Load(object sender, EventArgs e) { 
            AccountListForeColor( ); //calling this method to set the font colors in the list when the form is loaded
        }

        private string user_privilege_code() { // getting the value of user privilege in datagridview
            string code = "";        
            return code=dgvAccounts.CurrentRow.Cells["LoginAccessCode"].Value.ToString();
        }
        private void ShowAccountDetails() { // setting the privileges accordingly to the users privilege code, enabling/disabling the checkboxes user privilege

            txtUsername.Text = dgvAccounts.CurrentRow.Cells[ "Username" ].Value.ToString( );
            txtPassword.Text = dgvAccounts.CurrentRow.Cells[ "Password" ].Value.ToString( );
            account_type_textBox.Text = dgvAccounts.CurrentRow.Cells[ "Type" ].Value.ToString( );

            if( account_type_textBox.Text == "User" )
            {
                groupBox1.Enabled = true;

                if( user_privilege_code( ) == "a" )
                {
                    accountingCheckBox .Checked = true; costMonitoringCheckBox.Checked = false; procurementCheckBox .Checked = false; humanResourceCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "c" )
                {
                    costMonitoringCheckBox.Checked = true; accountingCheckBox .Checked = false; procurementCheckBox .Checked = false; humanResourceCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "p" )
                {
                    procurementCheckBox .Checked = true; accountingCheckBox .Checked = false; humanResourceCheckBox.Checked = false; costMonitoringCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "h" )
                {
                    humanResourceCheckBox.Checked = true; costMonitoringCheckBox.Checked = false; accountingCheckBox .Checked = false; procurementCheckBox .Checked = false;
                }
                else if( user_privilege_code( ) == "ca" )//
                {
                    accountingCheckBox .Checked = true; costMonitoringCheckBox.Checked = true; procurementCheckBox .Checked = false; humanResourceCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "ap" )
                {
                    accountingCheckBox .Checked = true; procurementCheckBox .Checked = true; humanResourceCheckBox.Checked = false; costMonitoringCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "ah" )
                {
                    accountingCheckBox .Checked = true; humanResourceCheckBox.Checked = true; costMonitoringCheckBox.Checked = false; procurementCheckBox .Checked = false;
                }
                else if( user_privilege_code( ) == "cp" )
                {
                    costMonitoringCheckBox.Checked = true; procurementCheckBox .Checked = true; accountingCheckBox .Checked = false; humanResourceCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "ch" )
                {
                    costMonitoringCheckBox.Checked = true; humanResourceCheckBox.Checked = true; accountingCheckBox .Checked = false; procurementCheckBox .Checked = false;
                }
                else if( user_privilege_code( ) == "ph" )
                {
                    procurementCheckBox .Checked = true; humanResourceCheckBox.Checked = true; accountingCheckBox .Checked = false; costMonitoringCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "cap" )
                {
                    accountingCheckBox .Checked = true; costMonitoringCheckBox.Checked = true; procurementCheckBox .Checked = true; humanResourceCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "cah" )
                {
                    accountingCheckBox .Checked = true; costMonitoringCheckBox.Checked = true; humanResourceCheckBox.Checked = true; procurementCheckBox .Checked = false;
                }
                else if( user_privilege_code( ) == "cap" )
                {
                    accountingCheckBox .Checked = true; procurementCheckBox .Checked = true; humanResourceCheckBox.Checked = true; costMonitoringCheckBox.Checked = false;
                }
                else if( user_privilege_code( ) == "cph" )
                {
                    costMonitoringCheckBox.Checked = true; procurementCheckBox .Checked = true; humanResourceCheckBox.Checked = true; accountingCheckBox .Checked = false;
                }
                else if( (user_privilege_code()=="aph") )
                {
                    accountingCheckBox.Checked = true; procurementCheckBox.Checked = true; humanResourceCheckBox.Checked = true; costMonitoringCheckBox.Checked = false;
                }
            }
            else
            {
                accountingCheckBox .Checked = true;
                costMonitoringCheckBox.Checked = true;
                procurementCheckBox .Checked = true;
                humanResourceCheckBox.Checked = true;
            }
        }

        int countCheck = 0;
        private void CheckedCheckBox( object sender , EventArgs e ) {
            CheckBox ch = ( CheckBox ) sender;

            if( ch.Checked )
            {
                countCheck = countCheck + 1; //if checkbox is to be checked increment by 1
                if( countCheck == 3 && account_type_textBox.Text == "User" )//if checkBox checked counts is 3 and account is user 
                {
                    DisableCheckBox( ); //Disable the 4th checkBox that is not yet checked
                    //PopupUserPrivilegeInfo( );//then show popup notification
                }
            }
            else if( ch.Checked == false )
            {
                countCheck = countCheck - 1;
                EnableCheckBox( );
            }
        }

        public void EnableCheckBox( ) {
            if( accountingCheckBox.Enabled == false )
            {
                accountingCheckBox.Enabled = true;
            }
            else if( costMonitoringCheckBox.Enabled == false )
            {
                costMonitoringCheckBox.Enabled = true;
            }
            else if( procurementCheckBox.Enabled == false )
            {
                procurementCheckBox.Enabled = true;
            }
            else if( humanResourceCheckBox.Enabled == false )
            {
                humanResourceCheckBox.Enabled = true;
            }
        }

        public void DisableCheckBox( ) {
            if( accountingCheckBox.Checked == false )
            {
                accountingCheckBox.Enabled = false;
            }
            else if( costMonitoringCheckBox.Checked == false )
            {
                costMonitoringCheckBox.Enabled = false;
            }
            else if( procurementCheckBox.Checked == false )
            {
                procurementCheckBox.Enabled = false;
            }
            else if( humanResourceCheckBox.Checked == false )
            {
                humanResourceCheckBox.Enabled = false;
            }
        }      

        private void btnCancel_Click(object sender, EventArgs e) {

            //if( Application.OpenForms.OfType<CreateAccountsForm>( ).Count( )==1 )
            //{
            //Application.OpenForms[ "CreateAccountsForm" ].Opacity = 1.0;            
            Close( );

            //} 

        }
        private void dgvAccounts_CellClick(object sender, DataGridViewCellEventArgs e) {
            ShowAccountDetails( );
        }       

        private void btRemove_Click(object sender, EventArgs e) {
            DialogResult remove = MessageBox.Show( "Are you sure you want to remove this account?" , "Remove account" , MessageBoxButtons.YesNoCancel , MessageBoxIcon.Question );

            if( remove == DialogResult.Yes )
            {
                RemoveAccount( );
                MessageBox.Show( "Account removed!" , "Successfuly removed" , MessageBoxButtons.OK , MessageBoxIcon.Information );
            }
        }
        private void RemoveAccount( ) {
            int id = Convert.ToInt32( dgvAccounts.CurrentRow.Cells[ "Id" ].Value );
            if( AccountTableName()=="Admin" )
            {
                AdminModel credential = new AdminModel( id);
                GlobalConfig.LoginGlobalConnection.RemoveAccount( SpLoginEventsList.spRemoveAccount , credential , AccountTableName( ) );               
            }
            else
            {
                UserModel credential = new UserModel( id );
                GlobalConfig.LoginGlobalConnection.RemoveAccount( SpLoginEventsList.spRemoveAccount , credential , AccountTableName( ) );
            }
                LoadAccounts( );
                AccountListForeColor( );                    
        }

        private void AdminControlForm_Activated(object sender, EventArgs e) {

            //AdminCountLbl.Text = UserCount.AdminCountMethod( ).ToString( );
            //UserCountLbl.Text = UserCount.UserCountMethod( ).ToString( );
          //  Form_opacity_fade.form_opacity_fade_in( this );
        }

        private void CheckBoxKeyDownCheck(object sender, KeyEventArgs e) {
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

        private DataTable GetAllAccounts( ) {
            return GlobalConfig.LoginGlobalConnection.GetAllAccounts( SpLoginEventsList.spGetAllAccounts );
        }

        private void SearchAccounts() {
            DataTable table = GetAllAccounts( );
            dgvAccounts.DataSource = table;
            DataView dv = new DataView( table );
            dv.RowFilter = string.Format( "Username LIKE '%{0}%'" , txtSearch.Text.Replace( "'" , "''" ) );
            dgvAccounts.DataSource = dv;

            AccountListForeColor( );

            dgvAccounts.Columns[ "Id" ].Visible = false;
            dgvAccounts.Columns[ "Password" ].Visible = false;
            dgvAccounts.Columns[ "LoginAccessCode" ].Visible = false;
            dgvAccounts.Columns[ "Type" ].Visible = false;

            dgvAccounts.Columns[ "Username" ].Width = dgvAccounts.Width;

            dgvAccounts.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dgvAccounts.Update( );
        }
        private void txtSearch_TextChanged(object sender, EventArgs e) {
            SearchAccounts( );
        }
    
        private void AdminControlForm_Click(object sender, EventArgs e) {
           
        }    

        private void minimize_pictureBox_Click( object sender , EventArgs e ) {
            WindowState = FormWindowState.Minimized;
        }

        private void close_pictureBox_Click( object sender , EventArgs e ) {
            Environment.Exit( Environment.ExitCode );
        }

        private void txtSearch_KeyDown( object sender , KeyEventArgs e ) {
            if( e.KeyCode==Keys.Enter )
            {
                SearchAccounts( );
            }
        }

        private void txtSearch_Click( object sender , EventArgs e ) {
            txtSearch.Clear( );
        }

        private void account_type_textBox_TextChanged( object sender , EventArgs e ) {
            if( account_type_textBox.Text == "Admin" )
            {
                groupBox1.Enabled = false;
                accountingCheckBox .Checked = true; costMonitoringCheckBox.Checked = true; procurementCheckBox .Checked = true; humanResourceCheckBox.Checked = true;

            }
            else
            {
                accountingCheckBox .Checked = false; costMonitoringCheckBox.Checked = false; procurementCheckBox .Checked = false; humanResourceCheckBox.Checked = false;
                groupBox1.Enabled = true;
                EnableCheckBox( );
            }
        }

        private void show_password_pictureBox_Click( object sender , EventArgs e ) {
            if( show_password_pictureBox.Visible == true )
            {
                show_password_pictureBox.Visible = false;
                txtPassword.PasswordChar = '\0';
                hide_password_pictureBox.Visible = true;
            }
        }

        private void hide_password_pictureBox_Click( object sender , EventArgs e ) {
            if( hide_password_pictureBox.Visible == true )
            {
                hide_password_pictureBox.Visible = false;
                txtPassword.PasswordChar = '▪';
                show_password_pictureBox.Visible = true;
            }
        }

        private void update_bunifuThinButton_Click( object sender , EventArgs e ) {
            if( ValidateInput( ) == true )
            {
                if( dgvAccounts.CurrentRow.Cells[ "Username" ].Value.ToString( ) == txtUsername.Text.Replace( "'" , "''" ) )//No changes in username
                {
                    if( dgvAccounts.CurrentRow.Cells[ "Type" ].Value.ToString( ) != account_type_textBox.Text )//Username remained and now check if account type is to be change
                    {
                        if( AdminUsernameExist( ) == false && account_type_textBox.Text == "Admin" )//
                        {
                            CreateAdmin( );
                            MessageBox.Show( "User account changed to Admin account" , "Update account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                            RemoveAccount( );
                        }
                        else if( UserUsernameExist( ) == false && account_type_textBox.Text == "User" )
                        {
                            CreateUser( );
                            MessageBox.Show( "Admin account changed to User account" , "Update account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                            RemoveAccount( );
                        }
                        else
                        {
                            MessageBox.Show( "This username was already been used, try another one" , "Username exist" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Information );
                        }

                    }
                    else
                    {
                        if( account_type_textBox.Text == "Admin" )
                        {
                            AdminUpdate( );
                            MessageBox.Show( "Admin account records updated" , "Update account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                        }
                        else if( account_type_textBox.Text == "User" )
                        {
                            UserUpdate( );
                            MessageBox.Show( "User account records updated" , "Update account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                        }
                        else
                        {
                            MessageBox.Show( "This username was already been used, try another one" , "Username exist" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Information );
                        }
                    }
                }
                else if( dgvAccounts.CurrentRow.Cells[ "Username" ].Value.ToString( ) != txtUsername.Text.Replace( "'" , "''" ) )
                {
                    if( dgvAccounts.CurrentRow.Cells[ "Type" ].Value.ToString( ) != account_type_textBox.Text )
                    {
                        if( AdminUsernameExist( ) == false && account_type_textBox.Text == "Admin" )
                        {
                            CreateAdmin( );
                            MessageBox.Show( "User account changed to Admin account" , "Update account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                            RemoveAccount( );
                        }
                        else if( UserUsernameExist( ) == false && account_type_textBox.Text == "User" )
                        {
                            CreateUser( );
                            MessageBox.Show( "Admin account changed to User account" , "Update account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                            RemoveAccount( );
                        }
                        else
                        {
                            MessageBox.Show( "This username was already been used, try another one" , "Username exist" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Information );
                        }
                    }
                    else
                    {                       
                        if( account_type_textBox.Text == "Admin" && AdminUsernameExist( ) == false && UserUsernameExist( ) == false )
                        {
                            AdminUpdate( );
                            MessageBox.Show( "Admin account records updated" , "Update account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                        }
                        else if( account_type_textBox.Text == "User" && UserUsernameExist( ) == false && AdminUsernameExist( ) == false )
                        {
                            UserUpdate( );
                            MessageBox.Show( "User account records updated" , "Update account" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                        }
                        else
                        {
                            MessageBox.Show( "This username was already been used, try another one" , "Username exist" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Information );
                        }
                    }
                }
                LoadAccounts( );
                AccountListForeColor( );
            }
            else
            {
                MessageBox.Show( "Invalid/missing account credentials, check and try again!" , "Invalid" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Warning );
            }
        }
     
        private bool ValidateInput( ) {

            bool validationResult = false;

            if( txtUsername.Text != string.Empty && txtPassword.Text != string.Empty && account_type_textBox.Text != string.Empty && UserAccessCode() != string.Empty || UserAccessCode()=="all" )
            {
                validationResult = true;
            }
            return validationResult;
        }
        private void AdminUpdate( ) {
            AdminModel credentials = new AdminModel( txtUsername.Text.Replace( "'" , "''" ) , txtPassword.Text.Replace( "'" , "''" ),account_type_textBox.Text , Convert.ToInt32( dgvAccounts.CurrentRow.Cells[ "id" ].Value ) );
            GlobalConfig.LoginAdminConnection.Update( SpLoginEventsList.spAccountUpdate , credentials, "AdminAccount" );            
        }

        private void UserUpdate( ) {
            UserModel credentials = new UserModel( txtUsername.Text.Replace( "'" , "''" ) , txtPassword.Text.Replace( "'" , "''" ),account_type_textBox.Text.Replace("'","''"), Convert.ToInt32( dgvAccounts.CurrentRow.Cells[ "id" ].Value ), UserAccessCode( ) );
            GlobalConfig.LoginUserConnection.Update( SpLoginEventsList.spAccountUpdate , credentials , "UserAccount" );
        }
        private bool AdminUsernameExist( ) {
            AdminModel credentials = new AdminModel(txtUsername.Text.Replace("'","''") );
            return GlobalConfig.LoginGlobalConnection.IsUsernameExist( SpLoginEventsList.spIsUsernameExist , credentials , "AdminAccount");
        }
        private bool UserUsernameExist( ) {
            UserModel credentials = new UserModel( txtUsername.Text.Replace("'","''"));
            return GlobalConfig.LoginGlobalConnection.IsUsernameExist( SpLoginEventsList.spIsUsernameExist , credentials , "UserAccount" );
        }     

        private void CreateAdmin( ) {
            AdminModel model = new AdminModel( txtUsername.Text.Replace( "'" , "''" ) , txtPassword.Text.Replace( "'" , "''" ) ,
                 account_type_textBox.Text.Replace( "'" , "''" ) );
            GlobalConfig.LoginAdminConnection.Create( SpLoginEventsList.spAccountCreate , model ,  "AdminAccount" );
        }
        private void CreateUser( ) {
            UserModel model = new UserModel( txtUsername.Text.Replace( "'" , "''" ) , txtPassword.Text.Replace( "'" , "''" ) ,
               account_type_textBox.Text.Replace( "'" , "''" ),UserAccessCode() );
            GlobalConfig.LoginUserConnection.Create( SpLoginEventsList.spAccountCreate , model ,  "UserAccount" );
        }
       
        private string AccountTableName( ) {
            string account = dgvAccounts.CurrentRow.Cells[ "Type" ].Value.ToString( );
            string result="";
           
            result = ( account == "Admin" ) ? "AdminAccount" : "UserAccount";
            return result;
        }

        private string UserAccessCode( ) {
            AccessCodesModel codes = new AccessCodesModel( costMonitoringCheckBox.Checked , accountingCheckBox.Checked , procurementCheckBox.Checked , humanResourceCheckBox.Checked );
            return GlobalConfig.LoginCodes.UserAccessCode( codes);
        }

        private void AdminControlForm_Deactivate( object sender , EventArgs e ) {
            //if( Application.OpenForms.OfType<CreateAdminAccountForm>( ).Count( ) == 1 )
            //{
         //     Application.OpenForms[ "AdminControlForm" ].Opacity = 0;
            //}

        }
    }
}
