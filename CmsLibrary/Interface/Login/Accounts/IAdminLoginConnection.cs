using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.Login {
    /// <summary>
    /// Contract or requirements for admin account only.
    /// </summary>
    public interface IAdminLoginConnection {

        /// <summary>
        /// Creates account
        /// </summary>
        /// <param name="credentials">properties values set in account credentials argument</param>
        /// <param name="tableName">table name of what account to be created "AdminAccount or UserAccount"</param>
        void Create( string events , AdminModel credentials , string tableName );

        /// <summary>
        /// Updates account
        /// </summary>
        /// <param name="credentials">properties values set in account credentials argument</param>
        /// <param name="tableName">table name of what account to be created "AdminAccount or UserAccount"</param>
        void Update( string events , AdminModel credentials , string tableName );

        /// <summary>
        /// Gets the record of admin accounts and return value to a DataTable
        /// </summary>
        /// <returns>DataTable</returns>
        DataTable AdminGetAccounts( string events  );
    }
}
