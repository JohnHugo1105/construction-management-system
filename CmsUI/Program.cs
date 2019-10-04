using CmsLibrary;
using GSG_Builders.RevisionedUI.Database_importation;
//using PropertiesLibrary.Login.StaticProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace GSG_Builders
{
    static class Program
       
    {
        public static bool run_system = false;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {            
              Application.EnableVisualStyles( );
              Application.SetCompatibleTextRenderingDefault( false );            
            GlobalConfig.InitializeConnections( ); //This is to initialize connection to our sql database,
                                                   // this will help to make only one instantiation
                                                   //Application.Run( new Login.CreateAdminAccountForm() );
                                                   //Application.Run( new Login.AdminControlForm( ) );
            Application.Run( new CreateAccountsForm( ) );
            //Application.Run( new RevisionedUI.Home.Home_Form( ) );
        }
    }
}
