using CmsLibrary.Interface.CostMonitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Model.CostMonitoring.ProjectSelection {
    public class SubProjectsModel : IProjectsCredentials {
        public DateTime DateStarted {
            get; set;
        }

        public int Id {
            get; set;
        }

        public int MainProjectId {
            get; set;
        }

        public string ProjectName {
            get; set;
        }
    }
}
