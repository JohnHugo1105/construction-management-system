using CmsLibrary.Interface.Login.SpEvents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary {
    public class LoginSpEventsModel : ISpEvents {       

        public string spCreate {
            get {
                return "AccountCreate";
            }
        }

        public string spUpdate {
            get {
                return "AccountUpdate";
            }
        }

        public string spGetAllAccounts {
            get {
                return "GetAllAccounts";
            }
        }

        public string spIsCredentialsValid {
            get {
                return "LoginValidationAccounts";
            }
        }

        public string spIsUsernameExist {
            get {
                return "UsernameExist";
            }
        }

        public string spRemoveAccount {
            get {
                return "RemoveAccount";
            }
        }     

        public string spAdminGetAccounts {
            get {
                return "AdminGetAccounts";
            }
        }

        public string spUserGetAccounts {
            get {
                return "UserGetAccounts";
            }
        }


    }
}
