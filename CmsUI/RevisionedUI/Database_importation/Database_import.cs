using GSG_Builders.RevisionedUI.Database_connection;
using GSG_Builders.RevisionedUI.Home;
using GSG_Builders.RevisionedUI.Wait_UI;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.Database_importation {
    class Database_import {

        BackgroundWorker bw = new BackgroundWorker( );
        string folder_param = "";
        public static bool importing = false;

        public void importing_database( ) {
            Server server = new Server( new ServerConnection( SystemInformation.ComputerName + @"\SQLEXPRESS" ) );

            //if( !server.Databases.Contains( "construction_management_system" ) )
            //{
            //if( !bw.IsBusy )
            //{
                MessageBox.Show( "Database not found, import by selecting the database BACPAC file " );
                //    importing = true;
                OpenFileDialog folder = new OpenFileDialog( );
                folder.Filter = "BACPAC files (*.bacpac)|*.bacpac";
                if( folder.ShowDialog( ) == DialogResult.OK )
                {
                    On_process_form process = new On_process_form( );
                    process.caption_process_label.Text = "Importing database please wait . . .";
                    process.TopMost = true;
                    process.Show( );

                    folder_param = folder.FileName;
                    bw.RunWorkerAsync( );
                }
                bw.DoWork += Bw_DoWork;
                bw.RunWorkerCompleted += Bw_RunWorkerCompleted;
            //}
           
            //}          
        }

        private void Bw_RunWorkerCompleted( object sender , RunWorkerCompletedEventArgs e ) {
            if( Application.OpenForms.OfType<On_process_form>( ).Count( ) == 1 )
            {
                importing = true;
                Application.OpenForms[ "On_process_form" ].Close( );                
                MessageBox.Show( "Database imported successfully!" , "Imported database" , MessageBoxButtons.OK , MessageBoxIcon.Information );
                Application.OpenForms[ "Home_Form" ].Enabled = true;
                Application.OpenForms[ "Home_Form" ].Activate( );
            }
        }

        private void Bw_DoWork( object sender , DoWorkEventArgs e ) {
            //DacServices ds = new DacServices( MS_SQL_SERVER_connection.Get_connection_string( ) );
            //ds.ImportBacpac( BacPackage.Load( Path.Combine( folder_param ) ) , "construction_management_system" , new DacImportOptions( ) );
        }
    }
}
