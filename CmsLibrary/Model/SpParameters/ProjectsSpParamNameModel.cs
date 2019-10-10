using CmsLibrary.Interface.SpParameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Model.SpParameters {
    public class ProjectsSpParamNameModel : IProjectsSpParamName {
        public string DateStarted {
            get {
                return "@DateStarted";
            }
        }

        public string events {
            get {
                return "@event";
            }
        }

        public string Id {
            get {
                return "@Id";
            }
        }

        public string MainProjectId {
            get {
                return "@MainProjectId";
            }
        }

        public string ProjectName {
            get {
                return "@ProjectName";
            }
        }

        public string SubProjectName {
            get {
                return "@SubProjectName";
            }
        }

        public string TableName {
            get {
                return "@TableName";
            }
        }


    }
}
