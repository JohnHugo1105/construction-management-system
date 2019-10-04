using GSG_Builders.RevisionedUI.Database_connection;
//using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Configuration;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using GSG_Builders.RevisionedUI.Reusable_codes;
using System.Collections.Specialized;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using System.IO;
using Microsoft.SqlServer.Dac;
using GSG_Builders.RevisionedUI.Database_importation;

namespace GSG_Builders {
    public partial class Form1 : Form {
       private Label label = new Label( );
        //private RevisionedUI.MainForm.UCMain uc;
        public Form1 ( ) {
            InitializeComponent( );
        }
        
        public static SqlConnection GetConnection ( ) {
            //  MySqlConnection conPath = new MySqlConnection( "server=sql12.freemysqlhosting.net;port=3306;username=sql12269894;password=DwfXC8Luv6" );

            
                              SqlConnection conPath = new SqlConnection( "SERVER=JOHN\\SQLEXPRESS;database=test_db;Trusted_Connection=true;" );
            try
            {
                conPath.Open( );
            }
            finally
            {
                conPath.Close( );
            }

            return conPath;
        }
        private void Form1_Load ( object sender , EventArgs e ) {
            //string QuerySelectData2 = "tes_sp";
            //SqlConnection con2 = GetConnection( );
            //tryH:\THESIS\GSG_Builders\GSG_Builders\RevisionedUI\Reports\Dashboard\main_report.rpt
            //{
            //    SqlCommand sqlcmd2 = new SqlCommand( QuerySelectData2 , con2 );
            //    SqlDataAdapter sda2 = new SqlDataAdapter( );
            //    sda2.SelectCommand = sqlcmd2;
            //    DataTable dtable2 = new DataTable( );
            //    sda2.Fill( dtable2 );

            //    dataGridView1.DataSource = dtable2;
            //    BindingSource bsource2 = new BindingSource( );
            //    bsource2.DataSource = dtable2;
            //    dataGridView1.Update( );

            //}
            //finally
            //{
            //    con2.Close( );
            //}

            //using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            //{
            //    using( SqlCommand cmd = new SqlCommand( "user_account_count_1w_condtion" , con ) )
            //    {
            //        cmd.CommandType = CommandType.StoredProcedure;

            //        cmd.Parameters.Add( "@table_name" , SqlDbType.VarChar ).Value = "user_accounts";
            //        con.Open( );

            //        SqlDataAdapter sda2 = new SqlDataAdapter( );
            //        sda2.SelectCommand = cmd;
            //        DataTable dtable2 = new DataTable( );
            //        sda2.Fill( dtable2 );

            //        dataGridView1.DataSource = dtable2;
            //        BindingSource bsource2 = new BindingSource( );
            //        bsource2.DataSource = dtable2;
            //        dataGridView1.Update( );             
            //    }               
            //}
            //    //this.label.Name = "label";
            //    //this.label.Location = new Point( 70 , 15 );
            // this.label.Size = new Size( 50 , 100 );
            // label.Text = "label";
            // this.label.Visible = true;
            // this.Controls.Add( label );



            //            int projcount = 0;

            //    MySqlCommand cmd = new MySqlCommand( );
            //    cmd.Connection = MySQL_connection.GetConnection( );
            //    cmd.Connection.Open( );
            //    cmd.CommandText = "SELECT bill_mat_id,main_project FROM construction_management_system.bill_mat";
            //    MySqlDataReader reader = cmd.ExecuteReader( );
            //    listView1.Columns.Add( "player",250 );


            //    while( reader.Read( ) )
            //    {              
            //        listView1.Items.Add( reader.GetString( 1 ).ToString( ),projcount );
            //        //    listView1.Items.Add( reader.GetString(1 ) );
            //        projcount++;
            //    }

            //    reader.Close( );
        }
        //int col = 0;
        //int row = 0;
        Backup_db back = new Backup_db( );
        private void button1_Click ( object sender , EventArgs e ) {
          
            //FolderBrowserDialog folder = new FolderBrowserDialog( );
            //if( folder.ShowDialog()==DialogResult.OK )
            //{
            //    folder_path = folder.SelectedPath;
            //    backgroundWorker1.RunWorkerAsync( );
            //}
            //   Backup_db.table_name_param = Fields_reusable_base_class.account_privilege_code_table;

            //back.export( Fields_reusable_base_class.account_privilege_code_table, Name );

            //FolderBrowserDialog folder = new FolderBrowserDialog( );
            //if( folder.ShowDialog( ) == DialogResult.OK )
            //{
            //    DacServices ds = new DacServices( MS_SQL_SERVER_connection.Get_connection_string( ) );
            //    ds.ExportBacpac( folder.SelectedPath , "construction_management_system" );
            //}

            #region other codes

            //using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            //{
            //    using( SqlCommand cmd = new SqlCommand( stored_procedures_list.stored_proc_all , con ) )
            //    {
            //        cmd.CommandType = CommandType.StoredProcedure;
            //        cmd.Connection.Open( );
            //        cmd.Parameters.AddWithValue( Fields_reusable_base_class.event_parameter , Fields_reusable_base_class.database_checking_event );
            //        cmd.Parameters.AddWithValue( Fields_reusable_base_class.file_path , @"D:\My Files\CMS_DB.BAK" );

            //        cmd.ExecuteNonQuery( );
            //    }
            //}

            //using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            //{
            //    using( SqlCommand cmd = new SqlCommand( @"BACKUP DATABASE  construction_management_system TO DISK = '" + @"D:\My Files\bill_mat.BAK" + "' " , con ) )
            //    {// "BACKUP DATABASE construction_management_system TO DISK = '"+ @"D:\My Files\bill_mat.BAK" + "' " , con
            //     // cmd.CommandType = CommandType.StoredProcedure;
            //        cmd.Connection.Open( );

            //        //   cmd.Parameters.AddWithValue( Fields_reusable_base_class.event_parameter , Fields_reusable_base_class.insert_event );
            //        //   cmd.Parameters.AddWithValue( Fields_reusable_base_class.table_parameter , Fields_reusable_base_class.user_accounts_table );
            //        //    cmd.Parameters.AddWithValue( Fields_reusable_base_class.col_insert_query , "account_privilegecode_level_ID,username,Password,user_type,user_privilege_code " );
            //        //    cmd.Parameters.AddWithValue( Fields_reusable_base_class.col_insert_values , " '" + AdminaccountPrivilegecodeLevelID( ) + "','" + txtUsername.Text.Trim( ).Replace( "'" , "''" ) + "','" + txtPassword.Text.Trim( ).Replace( "'" , "''" ) + "','" + "Admin".Trim( ) + "','" + "all".Trim( ) + "' " );

            //        #region old codes
            //        //cmd.CommandType = CommandType.StoredProcedure;
            //        //cmd.Connection.Open( );
            //        //cmd.Parameters.Add( Fields_reusable_base_class.table_parameter , SqlDbType.VarChar ).Value = Fields_reusable_base_class.user_accounts_table;
            //        //cmd.Parameters.Add( Fields_reusable_base_class.col_insert_query , SqlDbType.VarChar ).Value = "account_privilegecode_level_ID,username,Password,user_type,user_privilege_code ";
            //        //cmd.Parameters.Add( Fields_reusable_base_class.col_insert_values , SqlDbType.VarChar ).Value =" '"+ AdminaccountPrivilegecodeLevelID() + "','"+ txtUsername.Text.Trim().Replace( "'" , "''" ) + "','"+ txtPassword.Text.Trim( ).Replace( "'" , "''" ) + "','"+ "Admin".Trim( ) + "','"+ "all".Trim( ) + "' ";
            //        //cmd.Parameters.Add( Fields_reusable_base_class.other_query , SqlDbType.VarChar ).Value = string.Empty;             
            //        #endregion
            //        cmd.ExecuteNonQuery( );
            //        //   MessageBox.Show( "You have successfully created an admin account" , "Admin account created" , MessageBoxButtons.OK , MessageBoxIcon.Information );
            //    }
            //}

            //ServerConnection con = new ServerConnection( );
            //Server server = new Server( new ServerConnection( @"JOHN\SQLEXPRESS" ) );
            //Database db = server.Databases[ "construction_management_system" ];
            ////  StoredProcedure sp = db.StoredProcedures[ stored_procedures_list.stored_proc_all,"dbo" ];
            //Table tb = db.Tables["bill_mat"];
            //Scripter script = new Scripter( server );
            //script.Options.ScriptData = true;
            ////ScriptingOptions op = new ScriptingOptions( );
            ////op.ScriptData = true;
            ////op.ScriptSchema = true;
            ////op.ScriptDrops = false;
            //script.Options.ScriptData = true;
            //script.Options.ScriptSchema = true;
            //script.Options.ScriptDrops = false;
            //script.Options.DriPrimaryKey = true;
            //StringCollection sc = new StringCollection( );
            //foreach( string s in script.EnumScript( new Urn[ ] { tb.Urn } ) )
            //    //sb.Append( s );
            //    sc.Add(s);
            ////System.IO.StreamWriter fs = System.IO.File.CreateText( @"ScriptTable" );
            ////fs.Write( sb );
            ////fs.Close( );
            ////  Console.WriteLine(sc);


            ////   StringCollection sc = db.Tables["bill_mat"].Script( script );
            //StreamWriter file = new StreamWriter( "D:/My Files/bill_mat_backup.txt" );
            //foreach( string item in sc )
            ////{
            //    //Console.WriteLine( item );
            //         file.Write( item );
            ////}
            //file.Close( );
            //Console.WriteLine("FINISHED");
            //return sb.ToString( );
            //Backup source = new Backup( );
            //source.Action = BackupActionType.Database;
            //source.Database = "bill_mat";
            //BackupDeviceItem destination = new BackupDeviceItem( @"D:\My Files\bill_mat.BAK" , DeviceType.File );
            //source.Devices.Add( destination );
            //source.SqlBackup( server );
            //con.Disconnect( );
            //server.Databases[ "construction_management_system" ].Tables[ "bill_mat" ].Script( );

            //RevisionedUI.MainForm.UCMain uc = new RevisionedUI.MainForm.UCMain( );
            // uc.Location = new Point( 60 , 12 );
            //ucMain1 = new RevisionedUI.MainForm.UCMain( );
            //  this.uc.Location = new Point( 70 , 15 );
            //uc = new RevisionedUI.MainForm.UCMain( );
            //this.uc.Size = new Size( 50 , 100 );

            //this.uc.Visible = true;
            //tableLayoutPanel1.Controls.Add( uc , col , row );
            //// col++;
            //row++;
            #endregion
        }

        private void button2_Click( object sender , EventArgs e ) {
            StringBuilder build = new StringBuilder( );
            Console.WriteLine(DateTime.Now.ToLongTimeString());
            for( int i = 0 ; i < 1000000 ; i++ )
            {
                build.Append( "Kinginah mo");
            }
            Console.WriteLine( DateTime.Now.ToLongTimeString( ) );
          //  BllLibrary.Class.Login.Properties.AdminPropBll n = new BllLibrary.Class.Login.Properties.AdminPropBll("sada","sdas","asdas","asdasd","asdasda");
           
            //Login.CreateAdminAccountForm cm = new Login.CreateAdminAccountForm( );
            //CMS_Class.Class.Login.Implementations.Admin_imp ad = new CMS_Class.Class.Login.Implementations.Admin_imp( );
            //ad.CheckAdminAccount( cm);
            //    back.import( Fields_reusable_base_class.account_privilege_code_table );
            //using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            //{
            //    using( SqlCommand cmd = new SqlCommand( stored_procedures_list.stored_proc_all , con ) )
            //    {
            //        cmd.CommandType = CommandType.StoredProcedure;
            //        cmd.Connection.Open( );
            //        cmd.Parameters.AddWithValue( Fields_reusable_base_class.event_parameter , Fields_reusable_base_class.database_checking_event );
            //        //    cmd.Parameters.AddWithValue( Fields_reusable_base_class.file_path , @"D:\My Files\CMS_DB.BAK" );
            //        SqlDataReader read = cmd.ExecuteReader( );
            //        while( read.Read() )
            //        {
            //            if( read.IsDBNull(0) )
            //            {

            //   Console.WriteLine(db.DatabaseOptions);
            //            }
            //        }
            //        cmd.ExecuteNonQuery( );
            //    }
            //}
        }

        private void backgroundWorker1_DoWork( object sender , DoWorkEventArgs e ) {
            //back.export( Fields_reusable_base_class.bill_mat_table );
           
        //    Console.WriteLine("FINISHED");
        }

        private void backgroundWorker1_RunWorkerCompleted( object sender , RunWorkerCompletedEventArgs e ) {
       //     MessageBox.Show( "Test" );
        }

        private void backgroundWorker1_ProgressChanged( object sender , ProgressChangedEventArgs e ) {
            progressBar1.Value = e.ProgressPercentage;
        }
    }
}
