using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary {
    /// <summary>
    /// Admin account model
    /// </summary>
    public class AdminModel : IAccountCredentials {

        public int Id {
            get; set;
        }

        public string LoginAccessCode {
            get; set;
        }
      
        public string Password {
            get; set;
        }

        public string Type {
            get; set;
        }

        public string Username {
            get; set;
        }
              

        public AdminModel( ) {

        }

        /// <summary>
        /// To create admin account
        /// </summary>
        /// <param name="username">Username of the admin</param>
        /// <param name="password">Password of the admin</param>
        /// <param name="type">Admin</param>
        /// <param name="loginAccessCode">Default value code of "all"</param>
        public AdminModel( string username, string password, string type, string loginAccessCode = "all") {
            Username = username;
            Password = password;
            Type = type;
            LoginAccessCode = loginAccessCode;
        }

        /// <summary>
        /// To update the credentials of the admin account
        /// </summary>
        /// <param name="username">Username of account</param>
        /// <param name="password">Password of account</param>
        /// <param name="type">Admin</param>
        /// <param name="id">The unique identifier of the account record or its primary key</param>
        /// <param name="loginAccessCode">Default value code of "all"</param>
        public AdminModel( string username , string password, string type , int id, string loginAccessCode = "all" ) {
            Username = username;
            Password = password;
            Type = type;
            Id = id;
            LoginAccessCode = loginAccessCode;
        }

        /// <summary>
        /// For login validation of username and password if it exist and correct
        /// </summary>
        /// <param name="username">Username of account</param>
        /// <param name="password">Password of account</param>       
        public AdminModel(string username,string password ) {
            Username = username;
            Password = password;
        }
        /// <summary>
        /// Checks if username already exist to prevent duplication
        /// </summary>
        /// <param name="username">Admin of account</param>
        public AdminModel(string username ) {
            Username = username;
        }

        public AdminModel(int id ) {
            Id = id;
        }

    }
}
