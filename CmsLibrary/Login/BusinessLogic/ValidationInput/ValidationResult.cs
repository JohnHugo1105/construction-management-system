using CmsLibrary.Interface.Login;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.BusinessLogic.Login {
    public class ValidationResult : IValidateInputProcess {
        public bool IsValidInput( IValidateInputCredentials credentials ) {
            bool validationResult = false;

            if( credentials.Username != string.Empty && credentials.Password != string.Empty && credentials.Type != string.Empty )
            {
                if( credentials.LoginAccessCode != string.Empty && credentials.Type == "Admin" )
                {
                    validationResult = true;
                }
                else if( credentials.LoginAccessCode != string.Empty && credentials.Type == "User" )
                {
                    validationResult = true;
                }

            }
            return validationResult;
        }
    }
}
