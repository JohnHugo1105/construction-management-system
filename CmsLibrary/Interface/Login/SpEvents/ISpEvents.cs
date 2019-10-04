using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.Login.SpEvents {

    /// <summary>
    /// Interface contract for the stored procedure, list of properties of events of stored procedures
    /// </summary>
    public interface ISpEvents {
         string  spCreate {
            get;
        }

       string spUpdate {
            get;
        }

        string spAdminGetAccounts {
            get;
        }

        string spRemoveAccount {
            get;
        }

        string spGetAllAccounts {
            get;
        }

        string spIsCredentialsValid {
            get;
        }

        string spUserGetAccounts {
            get;
        }

        string spIsUsernameExist {
            get;
        }

    }
}
