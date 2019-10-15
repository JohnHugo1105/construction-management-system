using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring {
  public  interface ICostCredentials {
         int Id {
            get; set;
        }
         string Project {
            get; set;
        }
         string SubProject {
            get; set;
        }
         string DivisionNumber {
            get; set;
        }
         string Scope {
            get; set;
        }
         string SubScope {
            get; set;
        }
         string WbsCode {
            get; set;
        }
         string ItemDescription {
            get; set;
        }
         string MyProperty {
            get; set;
        }
         double Quantity {
            get; set;
        }
         string UnitMeasurement {
            get; set;
        }
        string EntryDate {
            get; set;
        }
        string Remarks {
            get; set;
        }
         decimal UnitCost {
            get; set;
        }
         decimal TotalAmount {
            get; set;
        }
         decimal VatExclusive {
            get; set;
        }
        decimal Vat {
            get; set;
        }

    }
}
