using CmsLibrary.Interface.CostMonitoring;
using CmsLibrary.Interface.Login;
using CmsLibrary.Model.CostMonitoring;
using CmsLibrary.Model.CostMonitoring.ProjectSelection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary {

    //This class is where contract or 
    //interfaces of the system are declared


    // TODO : Set the login form infront of the Main menu form when the system runs
    // TODO : Analyze again and re-think if more logic and functions can be break down to smaller pieces

    /// <summary>
    /// Interface contract for the Login functionality Globally
    /// </summary>
    public interface IAccountLoginConnection {

        /// <summary>
        ///  Deletes account
        /// </summary>
        /// <param name="id">unique identifier of the account or its primary key</param>
        /// <param name="tableName">database table name</param>
        void RemoveAccount( string events , IAccountCredentials id, string tableName );

        /// <summary>
        /// Merge all records from Admin and User accounts tables
        /// </summary>
        /// <returns>All accounts record</returns>
        DataTable GetAllAccounts( string events  );

        /// <summary>
        /// Checks and validates accounts username and password both Admin and User
        /// </summary>
        /// <param name="credentials">properties values set in account credentials argument</param>
        /// <returns>true of false</returns>
        bool IsCredentialsValid( string events , IAccountCredentials credentials );

        /// <summary>
        /// Checks if the username already exist
        /// </summary>
        /// <param name="credentials">properties values set in account credentials argument</param>
        /// <param name="tableName">table name</param>
        /// <returns>true or false</returns>
        bool IsUsernameExist( string events , IAccountCredentials credentials, string tableName);    

    }

    /// <summary>
    ///  Interface contract for the Project Selection functionality
    /// </summary>
    public interface IProjectsConnection {
        //DataTable tableMainProjects( );

        //DataTable tableSubProjects( );

        int GetLatestIdentifier( string tableName);

        void RemoveProject( IProjectsCredentials Id, string tableName, string events);
     
    }
   

    // TODO : Think of the requirements and flow of the Cost monitoring system, Re-think if the logic, functions and requirements can be break down to smaller pieces

    /// <summary>
    /// Interface contract for the Cost Monitoring functionality
    /// </summary>
    public interface ICostMonitoringConnection {
        void Remove( );

        void Search( );

        void Refresh( );

        void Report( );

        void ImportDb( );

        void ExportDb( );

        DataTable GetRecords( );
                

    }

    // TODO : Think of the requirements and flow of the Account system, Re-think if the logic, functions and requirements can be break down to smaller pieces

    /// <summary>
    /// Interface contract for the Accounting functionality
    /// </summary>
    public interface IAccountingConnection {

    }

    // TODO : Think of the requirements and flow of the Procurement system, Re-think if the logic, functions and requirements can be break down to smaller pieces

    /// <summary>
    /// Interface contract for the Procurement functionality
    /// </summary>
    public interface IProcurementConnection {

    }

    // TODO : Think of the requirements and flow of the Human Resource system, Re-think if the logic, functions and requirements can be break down to smaller pieces

    /// <summary>
    /// Interface contract for the Human Resource functionality
    /// </summary>
    public interface IHumanResourceConnection {

    }


}
