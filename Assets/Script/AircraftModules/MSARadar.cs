using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class MSARadar : RadarModule
    {
        public int scanOrderVertical = 3;
        public float scanSpeed = 20;
        private float xAngle;
        private float yAngle;
        private int scanDirX = 1;
        private int scanDirY = -1;

        public Vector3 PointerPosOnScreen
        {
            get { return new Vector3(xAngle, yAngle, 1); }
        }

        protected override void Awake()
        {
            base.Awake();
            defaultModes = new RadarMode[] { RadarMode.SRC, RadarMode.ACM, RadarMode.HMD };
        }
        // Use this for initialization
        void Start()
        {
            xAngle = -currentScanAngleX;
            yAngle = currentScanAngleY / 2;
        }

        private void FixedUpdate()
        {
            if (isRadarActived)
                UpdateScan();
        }

        public override void UpdateScan()
        {
            xAngle += scanDirX * scanSpeed * Time.fixedDeltaTime;
            if (xAngle >= currentScanAngleX && scanDirX == 1)
            {
                xAngle = 2 * currentScanAngleX - xAngle;
                scanDirX = -1;
                UpdateScanY();
            }
            else if (xAngle <= -currentScanAngleX && scanDirX == -1)
            {
                xAngle = -2 * currentScanAngleX - xAngle;
                scanDirX = 1;
                UpdateScanY();
            }
        }

        private void UpdateScanY()
        {
            if (yAngle >= currentScanAngleY / (scanOrderVertical - 1) && scanDirY == 1)
            {
                yAngle = currentScanAngleY;
                scanDirY = -1;
            }
            else if (yAngle <= -currentScanAngleY / (scanOrderVertical - 1) && scanDirY == -1)
            {
                yAngle = -currentScanAngleY / (scanOrderVertical - 1);
                scanDirY = 1;
            }
            yAngle += currentScanAngleY / (scanOrderVertical - 1) * scanDirY * 1.1f;
        }
    }
}
