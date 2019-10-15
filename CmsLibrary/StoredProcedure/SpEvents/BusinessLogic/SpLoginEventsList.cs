using CmsLibrary.Interface.Login.SpEvents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.BusinessLogic.SpEvents {

    /// <summary>
    /// Collections of events or methods in stored procedure
    /// </summary>
   public static class SpLoginEventsList {

        /// <summary>
        /// Sets the event in the stored procedure for creating a account 
        /// </summary>
        public static string spAccountCreate =GlobalConfig.LoginSpEvents.spAccountCreate;

        /// <summary>
        /// Sets the event in the stored procedure for updating a account 
        /// </summary>
        public static string spAccountUpdate = GlobalConfig.LoginSpEvents.spAccountUpdate;

        /// <summary>
        /// Sets the event in the stored procedure for getting all the accounts, admin and user 
        /// </summary>
        public static string spGetAllAccounts= GlobalConfig.LoginSpEvents.spGetAllAccounts;

        /// <summary>
        /// Sets the event in the stored procedure for validating the username and password
        /// </summary>
        public static string spIsCredentialsValid= GlobalConfig.LoginSpEvents.spIsCredentialsValid;

        /// <summary>
        /// Sets the event in the stored procedure for checking if the username already exists 
        /// </summary>
        public static string spIsUsernameExist= GlobalConfig.LoginSpEvents.spIsUsernameExist;

        /// <summary>
        /// Sets the event in the stored procedure for removing an account 
        /// </summary>
        public static string spRemoveAccount= GlobalConfig.LoginSpEvents.spRemoveAccount;

        /// <summary>
        /// Sets the event in the stored procedure for getting all the admin account 
        /// </summary>
        public static string spAdminGetAccounts= GlobalConfig.LoginSpEvents.spAdminGetAccounts;

        /// <summary>
        /// Sets the event in the stored procedure for getting all the user account
        /// </summary>
        public static string spUserGetAccounts= GlobalConfig.LoginSpEvents.spUserGetAccounts;

    }
}
