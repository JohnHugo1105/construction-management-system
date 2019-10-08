using CmsLibrary.Interface.CostMonitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Model.CostMonitoring {
    public class MainProjectsModel : IProjectsCredentials {
        public DateTime DateStarted {
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

        /// <summary>
        /// Default constructor
        /// </summary>
        public MainProjectsModel( ) {

        }

        /// <summary>
        /// To create Main project
        /// </summary>
        /// <param name="projectName">name of the project</param>
        /// <param name="dateStarted">date when it started</param>
        public MainProjectsModel(string projectName, DateTime dateStarted ) {
            ProjectName = projectName;
            DateStarted = dateStarted;
        }


    }
}
