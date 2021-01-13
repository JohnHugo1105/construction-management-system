using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using GSG_Builders.RevisionedUI.Journal;
using GSG_Builders.RevisionedUI.Bill_Mat;
using System.Speech.Synthesis;
using GSG_Builders.RevisionedUI.Database_importation;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using GSG_Builders.RevisionedUI.Reusable_codes;

namespace GSG_Builders.RevisionedUI.Home {
    public partial class Home_Form : Form  {

      public static  bool bill_mat_archiving_on_processfa=false;
      //  string StrBtnFrom = "";
   //     string user_privilege_code; //this will hold the user privlilege restriction access code
        
        public Home_Form() {
            InitializeComponent();
            //Database_import import = new Database_import( );
            //import.importing_database( );

            //timer_StartSlideShowHome.Stop( );
            //count = 0;
            //timer_StartSlideShowHome.Start( );
            //timer_StartSlideShowHome.Interval = 1000;
        
           

            bfChckbShowPassword.BackColor = Color.DimGray;
            bfChckbCreateinput.BackColor = Color.DimGray;
            bunifuMaterialTextbox1.Text = "ADMIN";
            mtAccessControl.SelectedTab = mtTPlogin;
            //toolStripbtn_home.Enabled = false;
        //    FormSize( );

        }

        private void AccessRestriction( ) {
            
            if( CreateAccountsForm.AccessCode == "a" )
            {
                cost_monitoring_toolStripDropDownButton.Enabled = false; Procurement_toolStripbtn.Enabled = false; HumanResource_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "c" )
            {
                Accounting_toolStripbtn.Enabled = false; Procurement_toolStripbtn.Enabled = false; HumanResource_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "p" )
            {
                Accounting_toolStripbtn.Enabled = false; HumanResource_toolStripbtn.Enabled = false; cost_monitoring_toolStripDropDownButton.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "h" )
            {
                cost_monitoring_toolStripDropDownButton.Enabled = false; Accounting_toolStripbtn.Enabled = false; Procurement_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "ca" )
            {
                Procurement_toolStripbtn.Enabled = false; HumanResource_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "ap" )
            {
                HumanResource_toolStripbtn.Enabled = false; cost_monitoring_toolStripDropDownButton.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "ah" )
            {
                cost_monitoring_toolStripDropDownButton.Enabled = false; Procurement_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "cp" )
            {
                Accounting_toolStripbtn.Enabled = false; HumanResource_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "ch" )
            {
                Accounting_toolStripbtn.Enabled = false; Procurement_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "ph" )
            {
                Accounting_toolStripbtn.Enabled = false; cost_monitoring_toolStripDropDownButton.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "cap" )
            {
                HumanResource_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "cah" )
            {
                Procurement_toolStripbtn.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "aph" )
            {
                cost_monitoring_toolStripDropDownButton.Enabled = false;
            }
            else if( CreateAccountsForm.AccessCode == "cph" )
            {
                Accounting_toolStripbtn.Enabled = false;
            }
            else
            {
                Accounting_toolStripbtn.Enabled = true;
                cost_monitoring_toolStripDropDownButton.Enabled = true;
                Procurement_toolStripbtn.Enabled = true;
                HumanResource_toolStripbtn.Enabled = true;
            }
        }

        private void FormSize( ) {
            Width = Screen.PrimaryScreen.WorkingArea.Width - 15;
            Height = Screen.PrimaryScreen.WorkingArea.Height - 15;
            Top = Screen.PrimaryScreen.WorkingArea.Top;
            Left = Screen.PrimaryScreen.WorkingArea.Left;
            StartPosition = FormStartPosition.CenterScreen;
        }
     

        public static  bool importing = false;
        Server server = new Server(new ServerConnection(SystemInformation.ComputerName + @"\SQLEXPRESS"));
        Methods_return_type re = new Methods_return_type( );
        private void LoginCreateForm_Activated( object sender , EventArgs e ) {
         

            //if( importing == false )
            //{
            //    if( !server.Databases.Contains( "CmsDB" ) )
            //    {
            //        Enabled = false;
            //        importing = true;
            //        Database_import import = new Database_import( );
            //        import.importing_database( );
            //    }
            //    else
            //    {
            //        home_toolStripButton.BackColor = Color.SlateGray;
            //        Form_opacity_fade.form_opacity_fade_in( this );

            //        if( staticProperties.LoginStatic == true )
            //        {
            //            this.Enabled = true;        
            //            AccountRestriction( ); //this is the if else condition that will read the string value of string variable user_privilege_code
            //            staticProperties.LoginStatic = false;                     

            //        }
            //        if( staticProperties.run_systemStatic == true )
            //        {
            //            CreateAdminAccountForm CAAF = new CreateAdminAccountForm( );
            //            CheckAdminAccountImpBLL CAA = new CheckAdminAccountImpBLL( CAAF);

            //            if( CAA.AdminAccountNotExistBLL()==true )
            //            {
            //                CAA.ShowCreateAdminAccountFormBLL( );
            //            }
            //            else
            //            {                          
            //                CreateAccountsForm pw = new CreateAccountsForm( );
            //                pw.ShowDialog( );
            //            }                           
            //        }
            //    }
            //}
            //else if( Database_import.importing == true )
            //{
            //    home_toolStripButton.BackColor = Color.SlateGray;
            //    Form_opacity_fade.form_opacity_fade_in( this );

            //    if( staticProperties.LoginStatic == true )
            //    {
            //        this.Enabled = true;
            //        AccountRestriction( ); //this is the if else condition that will read the string value of string variable user_privilege_code
            //        CreateAccountsForm.login = false;

            //        username_label.Text = CreateAccountsForm.user_name;
            //        account_type_label2.Text = re.account_type( CreateAccountsForm.user_name );
            //    }
            //    if( staticProperties.run_systemStatic == true )
            //    {
            //        CreateAdminAccountForm CAAF = new CreateAdminAccountForm( );
            //        CheckAdminAccountImpBLL CAA = new CheckAdminAccountImpBLL( CAAF );

            //        if( CAA.AdminAccountNotExistBLL( ) == true )
            //        {
            //            CAA.ShowCreateAdminAccountFormBLL( );
            //        }
            //        else
            //        {                    
            //            CreateAccountsForm pw = new CreateAccountsForm( );
            //            pw.ShowDialog( );
            //        }
            //    }
            //}
        }

        private void bunifuImageButton3_Click(object sender, EventArgs e) {
            Environment.Exit(Environment.ExitCode);
        }

        private void bfImgBtnMinimize_Click(object sender, EventArgs e) {
            WindowState = FormWindowState.Minimized;
        }     

        private void bunifuImageButton1_Click_1(object sender, EventArgs e) {
       
        }

        private void bunifuImageButton1_MouseHover(object sender, EventArgs e) {

        }

        private void bunifuElipse2_TargetControlResized(object sender, EventArgs e) {
          
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

        private void btnLogin_Click(object sender, EventArgs e) {
     
        }

        private void bfTxtUsername_Click(object sender, EventArgs e) {
           
        }

        private void bfTxtUsername_Enter(object sender, EventArgs e) {
            Bunifu.Framework.UI.BunifuTextbox bftxt = (Bunifu.Framework.UI.BunifuTextbox)sender;
            bftxt.text = ""; bftxt.ForeColor = Color.Black;
            
        }

        private void bfTxtUsername_Leave(object sender, EventArgs e) {
            Bunifu.Framework.UI.BunifuTextbox bftxt = (Bunifu.Framework.UI.BunifuTextbox)sender;
            
            Bunifu.Framework.UI.BunifuTextbox[] bftxtArray = new Bunifu.Framework.UI.BunifuTextbox[] {bfTxtLoginUsername,bfTxtLoginPassword,bfTxtCreateUsername,bfTxtCreatePassword,bfTxtAdminUsername,bfTxtAdminPassword };

            string username = "Username";
            string password = "Password";

            foreach (var item in bftxtArray)
            {
                if (bftxt.text == "")
                {
                    if (bftxt.Name.ToString().Contains("Username"))
                    {
                        bftxt.ForeColor = Color.DimGray;                        
                        bftxt.text = username;
                    }
                    else
                    {
                        bftxt.ForeColor = Color.DimGray;
                        bftxt.text = password;
                    }
                }
            }           
        }       
        private void mtAccessControl_Selected(object sender, TabControlEventArgs e) {
        
        }        
        private void bfTxtAdminUsername_MouseHover(object sender, EventArgs e) {

        }       
      //  int ClickBtnFeaturesCount = 0;       
        private void btnClickedFunctions(object sender, EventArgs e) {
           
            //ToolStripButton toolstripBtn = (ToolStripButton)sender;

            //string[] bfimgbtnArray = new string[] { "home","payroll", "costMonitoringpercentage", "journal","ledger","warehouseinventory","manpower","log","exit" };
            //if (!toolstripBtn.Name.Contains("exit"))
            //{
            //    for (int i = 0; i < bfimgbtnArray.Length; i++)
            //    {
            //        if (toolstripBtn.Name.Contains(bfimgbtnArray[i]))
            //        {     
            //            if (ClickBtnFeaturesCount == 0)//this means the 1st start of the software and it is by default on the home page
            //            {
            //                toolStripbtn_home.Enabled = true;
            //                StrBtnFrom = bfimgbtnArray[i];
            //                //if (bfimgbtnArray[i] == "costMonitoringpercentage")
            //                //{                            
            //                //    bunifuTransition1.HideSync(uCmain1);
            //                //    bunifuTransition2.ShowSync(uCjournal1);
            //                //}
            //                if (bfimgbtnArray[i] == "journal")
            //                {
            //                    bunifuTransition1.HideSync(ucMain1);
            //                    bunifuTransition2.ShowSync(uCjournal1);
            //                    toolStripbtn_journal.Enabled = false;
            //                }
            //                //else if (bfimgbtnArray[i] == "ledger")
            //                //{

            //                //}
            //                //else if (bfimgbtnArray[i] == "conseq")
            //                //{

            //                //}
            //                //else if (bfimgbtnArray[i] == "manpower")
            //                //{

            //                //}
            //                else if (bfimgbtnArray[i] == "log")
            //                {
            //                    bunifuTransition1.HideSync(ucMain1);
            //                    bunifuTransition2.ShowSync(bfCard_AccessibilityFeatures);
            //                    toolStripbtn_log.Enabled = false;
            //                }
            //            }
            //            else if (ClickBtnFeaturesCount == 1)
            //            {
            //                  if (StrBtnFrom == "home" && bfimgbtnArray[i] == "journal")
            //                {
            //                    toolStripbtn_home.Enabled = true;
            //                    bunifuTransition1.HideSync(ucMain1);
            //                    bunifuTransition2.ShowSync(uCjournal1);
            //                    toolStripbtn_journal.Enabled = false;
            //                }
            //                else if (StrBtnFrom == "journal" && bfimgbtnArray[i] == "home")
            //                {
            //                    toolStripbtn_journal.Enabled = true;
            //                    bunifuTransition1.HideSync(uCjournal1);
            //                    bunifuTransition2.ShowSync(ucMain1);
            //                    //uCmain1.Focus();
            //                    toolStripbtn_home.Enabled = false;
            //                }
            //                else if (StrBtnFrom == "log" && bfimgbtnArray[i] == "home")
            //                {
            //                    toolStripbtn_log.Enabled = true;
            //                    bunifuTransition1.HideSync(bfCard_AccessibilityFeatures);
            //                    bunifuTransition2.ShowSync(ucMain1);
            //                    //uCmain1.Focus();
            //                    toolStripbtn_home.Enabled = false;
            //                }
            //                else if (StrBtnFrom == "home" && bfimgbtnArray[i] == "log")
            //                {
            //                    toolStripbtn_home.Enabled = true;
            //                    bunifuTransition1.HideSync(ucMain1);
            //                    bunifuTransition2.ShowSync(bfCard_AccessibilityFeatures);
            //                    toolStripbtn_log.Enabled = false;
            //                }
            //                else if (StrBtnFrom == "journal" && bfimgbtnArray[i] == "log")
            //                {
            //                    toolStripbtn_journal.Enabled = true;
            //                    bunifuTransition1.HideSync(uCjournal1);
            //                    bunifuTransition2.ShowSync(bfCard_AccessibilityFeatures);
            //                    toolStripbtn_log.Enabled = false;
            //                }
            //                else if (StrBtnFrom == "log" && bfimgbtnArray[i] == "journal")
            //                {
            //                    toolStripbtn_log.Enabled = true;
            //                    bunifuTransition1.HideSync(bfCard_AccessibilityFeatures);
            //                    bunifuTransition2.ShowSync(uCjournal1);
            //                    toolStripbtn_journal.Enabled = false;
            //                }
            //                StrBtnFrom = bfimgbtnArray[i];
            //                ClickBtnFeaturesCount =0;                            
            //            }
            //        }
            //    }
            //    ClickBtnFeaturesCount += 1;               
            //}
            //else
            //{
            Environment.Exit( Environment.ExitCode );
            //}
        }               
        private void toolStripbtn_warehouseinventory_MouseHover(object sender, EventArgs e) {
            
        }

        private void toolStripbtn_home_MouseHover(object sender, EventArgs e) {
            toolStrip_Btnfeatures.Focus();
        }
      
        private void toolStripbtn_journal_Click(object sender, EventArgs e) {
          
        }
        private void Journal_FormClosed(object sender, FormClosedEventArgs e) {
            journal = null;
        }
       
        private void timer_Fadeout_Tick(object sender, EventArgs e) {           

            if (this.Opacity > 0.0)
            {
                this.Opacity -= 0.075;
            }
            else
            {
                timer_Fadeout_journal.Stop();
                this.Hide();
                this.Opacity = 1.0;
                journal = new Journal_form();
                journal.FormClosed += Journal_FormClosed;
                journal.Show();
            }
            //}                     
          
        }
       

        private void LoginCreateForm_MouseHover(object sender, EventArgs e) {
         
        }
        int count = 0;
        private void timer_StartSlideShowHome_Tick(object sender, EventArgs e) {            
            Console.WriteLine(count);
            count++;
            if( count==1 )
            {
                CreateAccountsForm create = new CreateAccountsForm( );
                create.ShowDialog( );
                timer_StartSlideShowHome.Stop( );
            }
            //if (this.Visible && count==1 )
            //{
            //    //SlideShowHome( );
            //}           
        }
        
        private void toolStripbtn_costmonitoringpercentage_Click ( object sender , EventArgs e ) {
           
        }
    //    int seconds = 0;
    //    bool login_form_show = false;
        private void cost_monitoring_timer_Tick ( object sender , EventArgs e ) {
            //if( this.Opacity > 0.0 )
            //{
            //    this.Opacity -= 0.075;
            //}
            //else
            //{
            //    cost_monitoring_timer.Stop( );
            //    this.Hide( );
            //    this.Opacity = 1.0;
            //    bill_mat = new BillMat_Form( );
            //    bill_mat.FormClosed += Bill_mat_FormClosed;
            //    bill_mat.Show( );
            //}
            //if( seconds == 5 )
            //{
            //    if( login_form_show == false )
            //    {
            //        CreateAccountsForm pw = new CreateAccountsForm( );
            //        pw.ShowDialog( );
            //        login_form_show = true;
            //        cost_monitoring_timer.Stop( );
            //    }
            //}
            //seconds++;
        }
        private void Bill_mat_FormClosed ( object sender , FormClosedEventArgs e ) {
            //bill_mat = null;
        }

        private void bill_Mat_ToolStripMenuItem_Click ( object sender , EventArgs e ) {
          
        }

        private void close_pictureBox_Click ( object sender , EventArgs e ) {
            Environment.Exit( Environment.ExitCode );
        }

        private void minimize_pictureBox_Click ( object sender , EventArgs e ) {
            WindowState = FormWindowState.Minimized;
        }

        private void settings_ToolStripMenuItem_Click ( object sender, EventArgs e ) {
            home_toolStripButton.BackColor = Color.SteelBlue;
            ProjectConfigForm New = new ProjectConfigForm( );
            New.Show( );
        }

        private void backgroundWorker1_DoWork ( object sender, DoWorkEventArgs e ) {        
            //SlideShowHome( );
        }

        private void Home_Form_KeyDown ( object sender , KeyEventArgs e ) {
            if( e.KeyCode==Keys.F2 )
            {
                toolStripbtn_costmonitoringpercentage_Click( this , new EventArgs());
            }
        }

        private void Home_Form_Leave ( object sender , EventArgs e ) {
            home_toolStripButton.BackColor = Color.SteelBlue;
        }
        Warehouse_inventory.inventory_form invent;
        private void toolStripbtn_warehouseinventory_Click ( object sender , EventArgs e ) {
            try
            {
                if( invent == null )
                {
                   warehouse_inventory_timer.Start( );
                }
                else
                {
                    invent.Activate( );
                    invent.WindowState = FormWindowState.Normal;
                }
            }
            catch( Exception ex )
            {

                MessageBox.Show( "Error! : " + ex.Message , "ERROR!" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Error );
            }
        }

        private void toolStripbtn_ledger_Click ( object sender , EventArgs e ) {

         
        }

        private void warehouse_inventory_timer_Tick( object sender , EventArgs e ) {
            //if( this.Opacity > 0.0 )
            //{
            //    this.Opacity -= 0.075;
            //}
            //else
            //{
            //    warehouse_inventory_timer.Stop( );
            //    this.Hide( );
            //    this.Opacity = 1.0;
            //    invent = new Warehouse_inventory.inventory_form( );
            //    invent.FormClosed += Invent_FormClosed;
            //    invent.Show( );
            //}
        }

        private void Invent_FormClosed( object sender , FormClosedEventArgs e ) {
            //invent = null;
        }
        //HR.HR_form hr;
        private void toolStripbtn_HR_Click( object sender , EventArgs e ) {
            //try
            //{
            //    if ( invent == null )
            //    {
            //        HR_timer.Start( );
            //    }
            //    else
            //    {
            //        hr.Activate( );
            //        hr.WindowState = FormWindowState.Normal;
            //    }
            //}
            //catch ( Exception ex )
            //{

            //    MessageBox.Show( "Error! : " + ex.Message , "ERROR!" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Error );
            //}
        }

        private void HR_timer_Tick( object sender , EventArgs e ) {
            //if ( this.Opacity > 0.0 )
            //{
            //    this.Opacity -= 0.075;
            //}
            //else
            //{
            //    HR_timer.Stop( );
            //    this.Hide( );
            //    this.Opacity = 1.0;
            //    hr = new HR.HR_form( );
            //    hr.FormClosed += Hr_FormClosed;
            //    hr.Show( );
            //}
        }

        private void Hr_FormClosed( object sender , FormClosedEventArgs e ) {
            //hr = null;
        }

        private void Home_Form_Load( object sender , EventArgs e ) {
            AccessRestriction( ); //this is the if else condition that will read the string value of string variable user_privilege_code

            //cost_monitoring_timer.Start( );
        }
         
        private void toolStripbtn_exit_Click( object sender , EventArgs e ) {
            SpeechSynthesizer speech = new SpeechSynthesizer( );
            speech.SpeakAsync( "Backup your data after all transactions are accomplished within the day" );
            DialogResult Yes_no = MessageBox.Show( "Are you sure you want to exit?" , "Exit system?" , MessageBoxButtons.YesNo , MessageBoxIcon.Question );
            if( Yes_no==DialogResult.Yes )
            {
                speech.SpeakAsyncCancel( new Prompt( "" ) );
                Environment.Exit( Environment.ExitCode );
            }
        }

        private void toolStripbtn_log_Click( object sender , EventArgs e ) {
            DialogResult Yes_no = MessageBox.Show( "Do you want to logout and exit(YES) or login different account(NO)?" , "Account logout" , MessageBoxButtons.YesNo , MessageBoxIcon.Question );
            if( Yes_no==DialogResult.No)
            {
                CreateAccountsForm acc = new CreateAccountsForm( );
                acc.ShowDialog( );
            }
            else
            {
                
                SpeechSynthesizer speech = new SpeechSynthesizer( );
                speech.SpeakAsync( "Backup your data after all transactions are accomplished within the day" );
                speech.SpeakAsyncCancel( new Prompt( "" ) );
                Yes_no = MessageBox.Show( "Are you sure you want to exit?" , "Exit system?" , MessageBoxButtons.OKCancel , MessageBoxIcon.Question );
                Environment.Exit( Environment.ExitCode );
               
            }
        }

        private void Home_Form_VisibleChanged( object sender , EventArgs e ) {
          
        }
        BillMat_Form bill_mat;
        private void estbillsOfMaterialsToolStripMenuItem_Click( object sender , EventArgs e ) {
            try
            {
                if( bill_mat == null )
                {
                    cost_monitoring_timer.Start( );
                }
                else
                {
                    bill_mat.Activate( );
                    bill_mat.WindowState = FormWindowState.Normal;
                }
            }
            catch( Exception ex )
            {

                MessageBox.Show( "Error! : " + ex.Message , "ERROR!" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Error );
            }
        }
        Journal_form journal;
        private void journalToolStripMenuItem_Click( object sender , EventArgs e ) {
            try
            {
                if( journal == null )
                {
                    timer_Fadeout_journal.Start( );
                }
                else
                {
                    journal.Activate( );
                    journal.WindowState = FormWindowState.Normal;
                }
            }
            catch( Exception ex )
            {
                MessageBox.Show( "Error! : " + ex.Message , "ERROR!" , MessageBoxButtons.RetryCancel , MessageBoxIcon.Error );
            }
        }

        private void Home_Form_Deactivate( object sender , EventArgs e ) {
            //Form_opacity_fade.form_opacity_fade( this );
        }

        private void panel3_MouseEnter( object sender , EventArgs e ) {
          
        }

        private void linkLabel1_LinkClicked( object sender , LinkLabelLinkClickedEventArgs e ) {
          
        }
    }
}
