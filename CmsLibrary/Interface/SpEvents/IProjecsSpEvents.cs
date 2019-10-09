using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring.ProjectSelection {
   public interface IProjecsSpEvents {
         string spMainProjectCreate {
            get;
        }

        string spSubProjectCreate {
            get;
        }
        string spMainProjectUpdate {
            get;
        }
        string spSubProjectUpdate {
            get;
        }
        string spGetMainProjects {
            get;
        }
        string spGetSubProjects {
            get;
        }
        string spGetAllProjects {
            get;
        }
        string spRemoveProjects {
            get;
        }
        string spMainProjectNameExist {
            get;
        }
        string spSubProjectNameExist {
            get;
        }

         string spLatestIdentifier {
            get; 
        }


    }
}
