using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using AeroSim.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.Cockpit.MFD
{
    public class MFDDataContext
    {
        public MFDModule module;
        public Aircraft aircraft;
        public MFDModule.ScreenRuntime runtime;
        public MFDModule.ScreenProperty property;
        public MFDGraphicHelper drawer;
        public MFDDrawer mfdDrawer;
    }
}
