using CmsLibrary.Model.CostMonitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring {
   public interface IJournalConnection : ICostMonitoringConnection {
        void Save(JournalModel credentials );

        void Update( JournalModel credentials );
    }
}
