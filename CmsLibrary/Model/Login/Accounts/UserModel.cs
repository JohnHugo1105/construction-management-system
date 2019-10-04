using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary {
    /// <summary>
    /// User account model
    /// </summary>
    public class UserModel : IAccountCredentials {
        public int Id {
            get; set;
        }

        public string LoginAccessCode {
            get; set;
        }

        public string Password {
            get; set;
        }

        public string Username {
            get; set;
        }

        public string Type {
            get; set;
        }       

        public UserModel( ) {

        }

        /// <summary>
        /// To create a user account
        /// </summary>
        /// <param name="username">Username of account</param>
        /// <param name="password">Password of account</param>
        /// <param name="type">User</param>
        /// <param name="loginAccessCode">User access code</param>
        public UserModel( string username , string password , string type , string loginAccessCode ) {
            Username = username;
            Password = password;
            Type = type;
            LoginAccessCode = loginAccessCode;
        }
        /// <summary>
        /// To update the credentials of the user account
        /// </summary>
        /// <param name="username">Username of account</param>
        /// <param name="password">Password of account</param>
        /// <param name="type">User</param>
        /// <param name="id">The unique identifier of the account record or its primary key</param>
        /// <param name="loginAccessCode"></param>
        public UserModel( string username, string password, string type,  int id, string loginAccessCode ) {
            Username = username;
            Password = password;
            Type = type;            
            Id = id;
            LoginAccessCode = loginAccessCode;
        }
    
        /// <summary>
        /// Checks if username already exist to prevent duplication
        /// </summary>
        /// <param name="username">Username of account</param>
        public UserModel(string username ) {
            Username = username;
        }

        public UserModel( int id) {
            Id = id;
        }

    }
}
