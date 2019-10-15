using CmsLibrary.Interface.Login;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CmsLibrary.Models {
    /// <summary>
    /// Access code model
    /// </summary>
   public class AccessCodesModel : IAccessCodesCredentials {
        public bool CostMonitoringCB {
            get; set;
        }

        public bool AccountingCB {
            get; set;
        }

        public bool ProcurementCB {
            get; set;
        }

        public bool HumanResourceCB {
            get; set;
        }        

        public Control CostMonitoringEnable {
            get; set;
        }

        public Control AccountingEnable {
            get; set;
        }

        public Control ProcurementEnable {
            get; set;
        }

        public Control HumanResourceEnable {
            get; set;
        }

        public AccessCodesModel( ) {

        }

        /// <summary>
        /// User account access code
        /// </summary>
        /// <param name="costMonitoringCB">Cost monitoring</param>
        /// <param name="accountingCB">Accounting</param>
        /// <param name="procurementEnabled">Procurement</param>
        /// <param name="humanResourceCB">Human Resource</param>
        public AccessCodesModel(bool costMonitoringCB, bool accountingCB, bool procurementCB, bool humanResourceCB) {
            CostMonitoringCB = costMonitoringCB;
            AccountingCB = accountingCB;
            ProcurementCB = procurementCB;
            HumanResourceCB = humanResourceCB;           
        }

        /// <summary>
        /// Process to prevent user account from selecting all or four checkBox for account accessibility to the system
        /// </summary>
        /// <param name="costMonitoringCB">Cost monitoring checkBoxChecked</param>
        /// <param name="accountingCB">Accounting checkBoxChecked</param>
        /// <param name="procurementCB">Procurement checkBoxChecked</param>
        /// <param name="humanResourceCB">Human resource checkBoxChecked</param>
        /// <param name="costMonitoringEnabled">Cost monitoring checkBoxEnabled</param>
        /// <param name="accountingEnabled">Accounting checkBoxEnabled</param>
        /// <param name="procurementEnabled">Procurement checkBoxEnabled</param>
        /// <param name="humanResourceEnabled">Human resource checkBoxEnabled</param>
        public AccessCodesModel( bool costMonitoringCB , bool accountingCB , bool procurementCB , bool humanResourceCB , Control costMonitoringEnabled , Control accountingEnabled , Control procurementEnabled , Control humanResourceEnabled ) {

            CostMonitoringCB = costMonitoringCB;
            AccountingCB = accountingCB;
            ProcurementCB = procurementCB;
            HumanResourceCB = humanResourceCB;

            CostMonitoringEnable = costMonitoringEnabled;
            AccountingEnable = accountingEnabled;
            ProcurementEnable = procurementEnabled;
            HumanResourceEnable = humanResourceEnabled;
        }

    }
}
