using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.SpParameters {
  public interface IProjectsSpParamName {
        string ProjectName {
            get;
        }

        string SubProjectName {
            get;
        }
        string DateStarted {
            get;
        }
        string Id {
            get;
        }
        string MainProjectId {
            get;
        }
        string TableName {
            get;
        }
        string events {
            get;
        }


    }
}
