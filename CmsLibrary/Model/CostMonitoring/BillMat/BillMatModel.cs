using CmsLibrary.Interface.CostMonitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Model.CostMonitoring {
    public class BillMatModel : ICostCredentials {
        public string DivisionNumber {
            get; set;
        }

        public string EntryDate {
            get; set;
        }

        public int Id {
            get; set;
        }

        public string ItemDescription {
            get; set;
        }

        public string MyProperty {
            get; set;
        }

        public string Project {
            get; set;
        }

        public double Quantity {
            get; set;
        }

        public string Remarks {
            get; set;
        }

        public string Scope {
            get; set;
        }

        public string SubProject {
            get; set;
        }

        public string SubScope {
            get; set;
        }

        public decimal TotalAmount {
            get; set;
        }

        public decimal UnitCost {
            get; set;
        }

        public string UnitMeasurement {
            get; set;
        }

        public decimal Vat {
            get; set;
        }

        public decimal VatExclusive {
            get; set;
        }

        public string WbsCode {
            get; set;
        }
    }
}
