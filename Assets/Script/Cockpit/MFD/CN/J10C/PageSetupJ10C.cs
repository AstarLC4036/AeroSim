using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.Cockpit.MFD.CN.J10C
{
    [CreateAssetMenu(fileName = "J10C Page Setup", menuName = "MFD/Page Setup/J10C", order = 0)]
    public class PageSetupJ10C : MFDPageSetup
    {
        [SerializeReference]
        public List<IMFD_Page> pageList = new List<IMFD_Page>()
        {
            new RadarBScopeMasterPage(),
            new RadarPPIMMasterPage(),
            new GimbalMasterPage(),
            new PowerplantSlavePage()
        };

        public override List<IMFD_Page> PageList => pageList;

        public override IMFD_Page[] CreatePage()
        {
            IMFD_Page[] outputList = new IMFD_Page[pageList.Count];
            for(int i = 0; i< pageList.Count; i++)
            {
                outputList[i] = Utils.MemoryUtils.Copy<IMFD_Page>(pageList[i]);
            }
            return outputList;
        }
    }
}
