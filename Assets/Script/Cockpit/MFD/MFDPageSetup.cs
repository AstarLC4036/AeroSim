using AeroSim.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Cockpit.MFD
{
    public abstract class MFDPageSetup : ScriptableObject
    {
        public abstract List<IMFD_Page> PageList { get; }
        public abstract IMFD_Page[] CreatePage();
    }
}