using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.Login {
    /// <summary>
    /// Contract or requirements for user account only.
    /// </summary>
    public interface IUserLoginConnection  {


        /// <summary>
        /// Creates account
        /// </summary>
        /// <param name="credentials">properties values set in account credentials argument</param>
        /// <param name="tableName">table name of what account to be created "UserAccount"</param>
        void Create( string events , UserModel credentials , string tableName );

        /// <summary>
        /// Updates account
        /// </summary>
        /// <param name="credentials">properties values set in account credentials argument</param>
        /// <param name="tableName">table name of what account to be created "UserAccount"</param>
        void Update( string events , UserModel credentials , string tableName );


        /// <summary>
        /// Gets all the record from the UserAccount table
        /// </summary>
        /// <returns>all user accounts</returns>
        DataTable UserGetAccounts( string events );
    }
}
