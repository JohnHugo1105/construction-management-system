using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.Database_connection {
    class MS_SQL_SERVER_connection {

        public static string Get_connection_string( ) {
            string connection = "";         
                try
                {
                //connection = @"Data Source=JOHN\SQLEXPRESS;Initial Catalog=construction_management_system;Integrated Security=True";
                 connection = " SERVER="+ SystemInformation.ComputerName + ConfigurationManager.ConnectionStrings[ "connection" ].ConnectionString;
              //  connection = @"Driver ={SQL Server Native Client 11.0}; Server=JOHNJOHN\SQLEXPRESS;Initial Catalog=construction_management_system;Integrated Security=True";
            }
                finally
                {
                            
                }
                return connection;
        }
    }
}
