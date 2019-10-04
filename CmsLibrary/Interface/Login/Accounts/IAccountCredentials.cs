using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary {
    /// <summary>
    /// Credentials or Account informations are declared here
    /// </summary>
   public interface IAccountCredentials {
        /// <summary>
        /// The unique indentifier two account models
        /// </summary>
         int Id {
            get; set;
        }

        /// <summary>
        /// Username of the account models
        /// </summary>
        string Username {
            get; set;
        }

        /// <summary>
        /// Password of the account models
        /// </summary>
        string Password {
            get; set;
        }

        /// <summary>
        /// Type of account
        /// </summary>
        string Type {
            get; set;
        }

        /// <summary>
        /// This is for the accessibility code for login of the account models
        /// </summary>
        string LoginAccessCode {
            get; set;
        }
       
    }
}
