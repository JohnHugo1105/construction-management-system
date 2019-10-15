using CmsLibrary.Interface.CostMonitoring.ProjectSelection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Model.SpEvents {
    public class ProjectsSpEventsModel : IProjecsSpEvents {
        public string spGetAllProjects {
            get {
                return "GetAllProjects";
            }
        }

        public string spGetMainProjects {
            get {
                return "GetMainProjects";
            }
        }

        public string spGetSubProjects {
            get {
                return "GetSubProjects";
            }
        }

        public string spLatestIdentifier {
            get {
                return "LatestIdentifier";
            }
        }

        public string spMainProjectCreate {
            get {
                return "MainProjectCreate";
            }
        }

        public string spMainProjectNameExist {
            get {
                return "MainProjectNameExist";
            }
        }

        public string spMainProjectUpdate {
            get {
                return "MainProjectUpdate";
            }
        }

        public string spRemoveProjects {
            get {
                return "RemoveProjects";
            }
        }

        public string spSubProjectCreate {
            get {
                return "SubProjectCreate";
            }
        }

        public string spSubProjectNameExist {
            get {
                return "SubProjectNameExist";
            }
        }

        public string spSubProjectUpdate {
            get {
                return "SubProjectUpdate";
            }
        }     


    }
}
