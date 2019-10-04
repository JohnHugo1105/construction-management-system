using CmsLibrary.Business_Layer.Login;
using CmsLibrary.BusinessLogic.Login;
using CmsLibrary.DataAccess;
using CmsLibrary.DataAccess.CostMonitoring;
using CmsLibrary.DataAccess.Login;
using CmsLibrary.Interface.Login;
using CmsLibrary.Interface.Login.SpEvents;
using System.Configuration;
using System.Windows.Forms;

namespace CmsLibrary {
    /// <summary>
    /// Configures the Access to the sql database and all methods
    /// </summary>
    public static class GlobalConfig {
        /// <summary>
        /// Static access to IAccountLoginConnection interface methods and its dependencies 
        /// </summary>
        public static IAdminLoginConnection LoginAdminConnection {
            get; private set;
        }

        /// <summary>
        /// Static access to IUserLoginConnection interface methods and its dependencies 
        /// </summary>
        public static IUserLoginConnection LoginUserConnection {
            get; private set;
        }

        /// <summary>
        /// Static access to IAccountLoginConnection interface global methods and its dependencies 
        /// </summary>
        public static IAccountLoginConnection LoginGlobalConnection {
            get; private set;
        }

        /// <summary>
        /// Static access to IAccessCodesProcess interface methods and its dependencies 
        /// </summary>
        public static IAccessCodesProcess LoginCodes {
            get; private set;
        }

        /// <summary>
        /// Static access to IValidateInputProcess interface methods and its dependencies 
        /// </summary>
        public static IValidateInputProcess LoginValidation {
            get; private set;
        }

        /// <summary>
        /// Static access to ICostMonitoringConnection interface methods and its dependencies 
        /// </summary>
        public static ICostMonitoringConnection Cost {
            get; private set;
        }

        /// <summary>
        /// Static access to ISpEvents interface methods and its dependencies 
        /// </summary>
        public static ISpEvents LoginSpEvents {
            get; private set;
        }

        /// <summary>
        /// Initializing the instance of the classes that implements methods
        /// </summary>
        public static void InitializeConnections( ) {

            GlobalSqlConnector sql = new GlobalSqlConnector( );
            LoginGlobalConnection = sql;

            AdminSqlConnector adminSql = new AdminSqlConnector( );
            LoginAdminConnection = adminSql;

            UserSqlConnector userSql = new UserSqlConnector( );
            LoginUserConnection = userSql;

            AccessCodesValue code = new AccessCodesValue( );
            LoginCodes = code;

            ValidationResult valid = new ValidationResult( );
            LoginValidation = valid;

            BillMatConnector bm = new BillMatConnector( );
            Cost = bm;

            JournalConnector journal = new JournalConnector( );
            Cost = journal;

            LoginSpEventsModel events = new LoginSpEventsModel( );
            LoginSpEvents = events;

        }

        /// <summary>
        /// Static connection string in app.config file
        /// </summary>
        private static string ConnectionString = " SERVER=" + SystemInformation.ComputerName + ConfigurationManager.ConnectionStrings[ "connection" ].ConnectionString;

        /// <summary>
        /// Returning the connection string
        /// </summary>
        public static string ConnString {
            get {
                return ConnectionString;
            }
        }
    }
}
