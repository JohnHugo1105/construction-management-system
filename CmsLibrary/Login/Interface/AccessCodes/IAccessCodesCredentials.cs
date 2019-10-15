using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CmsLibrary.Interface.Login {
    /// <summary>
    /// This is where credentials or values needed for the Login processes are declared.
    /// </summary>
   public interface IAccessCodesCredentials {
         bool CostMonitoringCB {
            get; set;
        }

         bool AccountingCB {
            get; set;
        }

         bool ProcurementCB {
            get; set;
        }

         bool HumanResourceCB {
            get; set;
        }
        Control CostMonitoringEnable {
            get;set;
        }

        Control AccountingEnable {
            get; set;
        }

        Control ProcurementEnable {
            get; set;
        }

        Control HumanResourceEnable {
            get; set;
        }

    }
}
