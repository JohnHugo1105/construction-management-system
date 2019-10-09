using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.BusinessLogic.SpEvents {
   public static class SpProjectsEventsList {

        public static string spGetAllProjects = GlobalConfig.ProjectsSpEvents.spGetAllProjects;

        public static string spGetMainProjects = GlobalConfig.ProjectsSpEvents.spGetMainProjects;

        public static string spGetSubProjects = GlobalConfig.ProjectsSpEvents.spGetSubProjects;

        public static string spMainProjectCreate = GlobalConfig.ProjectsSpEvents.spMainProjectCreate;

        public static string spMainProjectNameExist = GlobalConfig.ProjectsSpEvents.spMainProjectNameExist;

        public static string spMainProjectUpdate = GlobalConfig.ProjectsSpEvents.spMainProjectUpdate;

        public static string spRemoveProjects = GlobalConfig.ProjectsSpEvents.spRemoveProjects;

        public static string spSubProjectCreate = GlobalConfig.ProjectsSpEvents.spSubProjectCreate;

        public static string spSubProjectNameExist = GlobalConfig.ProjectsSpEvents.spSubProjectNameExist;

        public static string spSubProjectUpdate = GlobalConfig.ProjectsSpEvents.spSubProjectUpdate;

        public static string spLatestIdentifier = GlobalConfig.ProjectsSpEvents.spLatestIdentifier;

    }
}
