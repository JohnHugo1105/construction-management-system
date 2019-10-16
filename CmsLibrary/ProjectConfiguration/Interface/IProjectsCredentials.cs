using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring {
   public interface IProjectsCredentials {
         int Id {
            get; set;
        }

         string ProjectName {
            get; set;
        }        

         DateTime DateStarted {
            get; set;
        }

    }
}
