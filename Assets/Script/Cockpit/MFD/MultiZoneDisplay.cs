using AeroSim.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Cockpit.MFD
{
    [Serializable]
    public class MultiZoneDisplay : MFDDrawer
    {
        public MFDPageSetup pageSetup;
        [SerializeReference]
        public List<IMFD_Page> mfdPages;
        [SerializeReference]
        public List<IMFD_Page> mfdPagesInSlot;
        private MFDDataContext dataContext;

        public MultiZoneDisplay(Vector2Int size, Color bgColor) : base(size, bgColor)
        {
            mfdPages = new List<IMFD_Page>();
            mfdPagesInSlot = new List<IMFD_Page>();
        }

        public void Init(MFDDataContext ctx, MFDPageSetup pageSetup)
        {
            dataContext = ctx;
            this.pageSetup = pageSetup;
            InitCanvas();
            drawer.SetDesignSize(ctx.property.size);
            CreatePages();

            //initial slots
            mfdPagesInSlot.Clear();
            foreach (MFD_PageType type in ctx.property.initialPages)
            {
                IMFD_Page page = mfdPages.Find(p => p.Type == type);
                mfdPagesInSlot.Add(page);
            }
        }

        public void CreatePages()
        {
            mfdPages.AddRange(pageSetup.CreatePage());
            foreach (var page in mfdPages)
            {
                page.Init(dataContext, drawer);
            }
        }

        public void UpdateSlot()
        {
            mfdPagesInSlot.Clear();
            //...
        }

        public override void ProcessCanvas()
        {
            foreach(var page in mfdPagesInSlot)
            {
                //drawer.SetClipRect(page.Zone);
                page.DrawPage();
                //drawer.ClearClip();
            }
        }
    }
}