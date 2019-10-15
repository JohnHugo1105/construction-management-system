using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.Login {
  public  interface IValidateInputCredentials {
         string LoginAccessCode {
            get; set;
        }

         string Password {
            get; set;
        }

         string Type {
            get; set;
        }

         string Username {
            get; set;
        }
    }
}
