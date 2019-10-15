using CmsLibrary.Model.CostMonitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmsLibrary.Interface.CostMonitoring {
   public interface IBillMatConnection: ICostMonitoringConnection {
        void Save(BillMatModel credentials);

        void Update(BillMatModel credentials);
    }
}
