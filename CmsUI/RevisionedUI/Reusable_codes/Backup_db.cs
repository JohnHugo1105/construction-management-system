using GSG_Builders.RevisionedUI.Database_connection;
using GSG_Builders.RevisionedUI.Wait_UI;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.Reusable_codes {
    class Backup_db {
        BackgroundWorker bw = new BackgroundWorker( );
        FolderBrowserDialog folder = new FolderBrowserDialog( );
        string folder_path = "";
        string table_name_param = "";
        string form_name_param = "";
        public void export(string table_name,string form_name ) {
            FolderBrowserDialog folder = new FolderBrowserDialog( );
            if( folder.ShowDialog( ) == DialogResult.OK )
            {
                form_name_param = form_name;
                Application.OpenForms[ form_name ].Enabled = false;
                On_process_form process = new On_process_form( );
                process.Show( );

                folder_path = folder.SelectedPath;
                bw.RunWorkerAsync( );           
            }
                table_name_param = table_name;
                bw.WorkerReportsProgress = true;
                bw.WorkerSupportsCancellation = true;

                bw.DoWork += Bw_DoWork;
                bw.ProgressChanged += Bw_ProgressChanged;
                bw.RunWorkerCompleted += Bw_RunWorkerCompleted;
            }

        private void Bw_RunWorkerCompleted( object sender , RunWorkerCompletedEventArgs e ) {
            if( Application.OpenForms.OfType<On_process_form>( ).Count( ) == 1 )
            {
                Application.OpenForms[ "On_process_form" ].Close( );
                Application.OpenForms[ form_name_param ].Enabled = true;
                MessageBox.Show( "Records backup successfully!" , "Backup record" , MessageBoxButtons.OK , MessageBoxIcon.Information );
            }
        }

        private void Bw_ProgressChanged( object sender , ProgressChangedEventArgs e ) {
            
        }

        private void Bw_DoWork( object sender , DoWorkEventArgs e ) {
            Server server = new Server( new ServerConnection( SystemInformation.ComputerName + @"\SQLEXPRESS" ) );
            Database db = server.Databases[ "construction_management_system" ];
            Table tb = db.Tables[table_name_param ];
            Scripter script = new Scripter( server );
            script.Options.ScriptData = true;
            script.Options.ScriptData = true;
            script.Options.ScriptSchema = true;
            script.Options.ScriptDrops = false;
            script.Options.DriPrimaryKey = true;

            StringBuilder sc = new StringBuilder( );
            StreamWriter file = new StreamWriter( folder_path + "/ " + table_name_param + "_backup.sql" );
            foreach( string s in script.EnumScript( new Urn[ ] { tb.Urn } ) )
            {
                file.Write( s + " " + Environment.NewLine );               
            }
            file.Close( );
        }

        public void import(string table_name ) {
            Stream stream;
            string if_exist = "IF OBJECT_ID('" + table_name + "','" + "U" + "') IS NOT NULL DROP TABLE "+table_name+"; ";
            OpenFileDialog dialog = new OpenFileDialog( );

            DialogResult YES = MessageBox.Show( "Are you sure you want to restore previous records?" + Environment.NewLine + "Existing records will be overwritten"
               , "Restore records" , MessageBoxButtons.YesNo , MessageBoxIcon.Question );
            if( YES == DialogResult.Yes )
            {
                if( dialog.ShowDialog( ) == DialogResult.OK )
                {
                    if( ( stream = dialog.OpenFile( ) ) != null )
                    {
                        string strfilename = dialog.FileName;
                        string filetext = File.ReadAllText( strfilename );
                        using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
                        {
                            using( SqlCommand cmd = new SqlCommand( if_exist + " " + Environment.NewLine + filetext ) )
                            {
                                cmd.Connection = con;
                                cmd.Connection.Open( );
                                cmd.ExecuteNonQuery( );
                                MessageBox.Show( "Records restored successfully!" , "Restore record" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                            }
                        }
                    }
                }
            }
        } 
    }
}
