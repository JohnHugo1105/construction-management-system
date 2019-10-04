using CmsLibrary.Interface.CostMonitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Model.CostMonitoring {
    public class MainProjectsModel : IProjectsCredentials {
        public string DateStarted {
            get; set;
        }

        public int Id {
            get; set;
        }

        public string ProjectName {
            get; set;
        }

        public string SubProjectName {
            get; set;
        }


    }
}
