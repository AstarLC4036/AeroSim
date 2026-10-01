using AeroSim.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace AeroSim.Cockpit.MFD
{
    public interface IMFD_Page
    {
        public Rect Zone { get; }
        public MFD_PageType Type { get; }
        public void DrawPage();
        public void Init(MFDDataContext dataCtx, MFDGraphicHelper drawer);
        public void OnButtonClick(int buttonIndex);
    }

    public enum MFD_PageType
    {
        RadarBScope,
        RadarPPI,
        Gimbal,
        Powerplant,
    }
}
