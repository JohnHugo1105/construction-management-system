using CmsLibrary.Interface.Login;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Model.Login {
    public class ValidateInputModel : IValidateInputCredentials {
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

        public ValidateInputModel( ) {

        }

        public ValidateInputModel(string username, string password, string type, string loginAccessCode ) {
            Username = username;
            Password = password;
            Type = type;
            LoginAccessCode = loginAccessCode;
        }
    }
}
