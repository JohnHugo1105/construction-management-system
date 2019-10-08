using CmsLibrary;
using CmsLibrary.BusinessLogic.SpEvents;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GSG_Builders.Login {
    public partial class CreateAdminAccountForm : Form {



        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
           int LeftEdge,
           int TopEdge,
           int Width,
           int Height,
           int TopEdgeCurvature,
           int BottomEdgeCurvature

           );

        public CreateAdminAccountForm() {
            InitializeComponent();

            //this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 25, 25)); // setting the rounded corners of the form
        }

        private void LoginForm_Load(object sender, EventArgs e) {
        
        }
             
        #region Dragging function form borderless
        /* this is to set a drag function to the form without a formborder */
        private bool mouseDown;
        private Point lastLocation;

        private void LoginForm_MouseDown(object sender, MouseEventArgs e) {
            mouseDown = true;
            lastLocation = e.Location;
        }

        private void LoginForm_MouseMove(object sender, MouseEventArgs e) {
            if (mouseDown)
            {
                this.Location = new Point(
                    (this.Location.X - lastLocation.X) + e.X, (this.Location.Y - lastLocation.Y) + e.Y);

                this.Update();
            }
        }

        private void LoginForm_MouseUp(object sender, MouseEventArgs e) {
            mouseDown = false;
        }
        /*ENDS HERE*/
        #endregion

        private void btnCreateUserAccount_Click(object sender, EventArgs e) {
            Create( );
        }

        private void Create( ) {
            AdminModel model = new AdminModel( txtUsername.Text.Replace( "'" , "''" ) , txtPassword.Text.Replace( "'" , "''" ) ,
                "Admin" );
            GlobalConfig.LoginAdminConnection.Create( SpLoginEventsList.spAccountCreate , model , "AdminAccount" );
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

        private void close_pictureBox_Click( object sender , EventArgs e ) {
            Environment.Exit( Environment.ExitCode );
        }

        private void minimize_pictureBox_Click( object sender , EventArgs e ) {
            WindowState = FormWindowState.Minimized;
        }

        private void cancel_bunifuThinButton23_Click( object sender , EventArgs e ) {
            Environment.Exit( Environment.ExitCode );
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
    }
}
