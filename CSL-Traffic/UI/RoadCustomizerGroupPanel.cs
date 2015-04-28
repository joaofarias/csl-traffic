using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSL_Traffic.UI
{
    class RoadCustomizerGroupPanel : GeneratedGroupPanel
    {
        protected override int GetCategoryOrder(string name)
        {
            switch (name)
            {
                case "VehicleRestrictions":
                    return 0;
                case "SpeedRestrictions":
                    return 1;
                default:
                    return int.MaxValue;
            }
        }
    }
}
