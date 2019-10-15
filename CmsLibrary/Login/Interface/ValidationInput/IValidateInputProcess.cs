using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.Login {

    /// <summary>
    /// Interface contract for the Validating functionality when a user tries to login, verifies if the input are correct
    /// </summary>
    public interface IValidateInputProcess {
        bool IsValidInput(IValidateInputCredentials valid );
    }
}
