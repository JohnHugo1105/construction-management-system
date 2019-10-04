using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.Login {
    /// <summary>
    /// This is where the Business logic or processes for login is.
    /// </summary>
    public interface IAccessCodesProcess {
        /// <summary>
        /// Validates what access codes for the account to be created that will be the accessibility for the system
        /// </summary>
        /// <param name="accessCode">Checkboxes for each department</param>
        /// <returns>String access code value</returns>
        string UserAccessCode( IAccessCodesCredentials accessCode );

        /// <summary>
        /// Determines whether to enable the check box when 3 access checkbox been checked and user account is chosen
        /// </summary>
        /// <param name="checkBoxCheck">CheckBoxChecked arguments</param>
        void EnableCheckBox( IAccessCodesCredentials checkBoxCheck );

        /// <summary>
        /// Determines whether to disable the check box when 3 access checkbox been checked and user account is chosen
        /// </summary>
        /// <param name="checkBoxEnable">CheckBox.Enabled arguments</param>
        void DisableCheckBox( IAccessCodesCredentials checkBoxEnable );
    }
}
