using CmsLibrary.Interface.Login;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Business_Layer.Login {
    public class AccessCodesValue : IAccessCodesProcess {
        public void DisableCheckBox( IAccessCodesCredentials checkBox ) {
            if( checkBox.AccountingCB == false )
            {
                checkBox.AccountingEnable.Enabled = false;
            }
            else if(checkBox.CostMonitoringCB == false )
            {
                checkBox.CostMonitoringEnable.Enabled = false;
            }
            else if(checkBox.ProcurementCB == false )
            {
                checkBox.ProcurementEnable.Enabled = false;
            }
            else if(checkBox.HumanResourceCB == false )
            {
                checkBox.HumanResourceEnable.Enabled = false;
            }
        }

        public void EnableCheckBox( IAccessCodesCredentials checkBox ) {
            if( checkBox.AccountingEnable.Enabled == false )
            {
                checkBox.AccountingEnable.Enabled = true;
            }
            else if( checkBox.CostMonitoringEnable.Enabled == false )
            {
                checkBox.CostMonitoringEnable.Enabled = true;
            }
            else if( checkBox.ProcurementEnable.Enabled == false )
            {
                checkBox.ProcurementEnable.Enabled = true;
            }
            else if( checkBox.HumanResourceEnable.Enabled == false )
            {
                checkBox.HumanResourceEnable.Enabled = true;
            }
        }

        public string UserAccessCode( IAccessCodesCredentials accessCode ) {
            string code = "";

            if( accessCode.CostMonitoringCB && accessCode.AccountingCB == false && accessCode.ProcurementCB == false && accessCode.HumanResourceCB == false )
            {
                code = "c";
            }
            else if( accessCode.AccountingCB && accessCode.CostMonitoringCB == false && accessCode.ProcurementCB == false && accessCode.HumanResourceCB == false )
            {
                code = "a";
            }
            else if( accessCode.ProcurementCB && accessCode.CostMonitoringCB == false && accessCode.HumanResourceCB == false && accessCode.AccountingCB == false )
            {
                code = "p";
            }
            else if( accessCode.HumanResourceCB && accessCode.AccountingCB == false && accessCode.CostMonitoringCB == false && accessCode.ProcurementCB == false )
            {
                code = "h";
            }
            else if( accessCode.CostMonitoringCB && accessCode.AccountingCB && accessCode.ProcurementCB == false && accessCode.HumanResourceCB == false )
            {
                code = "ca";
            }
            else if( accessCode.CostMonitoringCB && accessCode.ProcurementCB && accessCode.HumanResourceCB == false && accessCode.AccountingCB == false )
            {
                code = "cp";
            }
            else if( accessCode.CostMonitoringCB && accessCode.HumanResourceCB && accessCode.AccountingCB == false && accessCode.ProcurementCB == false )
            {
                code = "ch";
            }
            else if( accessCode.AccountingCB && accessCode.ProcurementCB && accessCode.CostMonitoringCB == false && accessCode.HumanResourceCB == false )
            {
                code = "ap";
            }
            else if( accessCode.AccountingCB && accessCode.HumanResourceCB && accessCode.CostMonitoringCB == false && accessCode.ProcurementCB == false )
            {
                code = "ah";
            }
            else if( accessCode.ProcurementCB && accessCode.HumanResourceCB && accessCode.CostMonitoringCB == false && accessCode.AccountingCB == false )
            {
                code = "ph";
            }
            else if( accessCode.CostMonitoringCB && accessCode.AccountingCB && accessCode.ProcurementCB && accessCode.HumanResourceCB == false )
            {
                code = "cap";
            }
            else if( accessCode.CostMonitoringCB && accessCode.AccountingCB && accessCode.HumanResourceCB && accessCode.ProcurementCB == false )
            {
                code = "cah";
            }
            else if( accessCode.CostMonitoringCB && accessCode.ProcurementCB && accessCode.HumanResourceCB && accessCode.AccountingCB == false )
            {
                code = "cph";
            }
            else if( accessCode.AccountingCB && accessCode.ProcurementCB && accessCode.HumanResourceCB && accessCode.CostMonitoringCB == false )
            {
                code = "aph";
            }
            else if( accessCode.AccountingCB == false && accessCode.ProcurementCB == false && accessCode.HumanResourceCB == false && accessCode.CostMonitoringCB == false )
            {
                code = "";
            }
            else
            {
                code = "all";
            }
            return code;
        }
    }
}
