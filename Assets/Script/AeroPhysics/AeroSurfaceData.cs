using System.Collections;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace AeroSim.AeroPhysics
{
    [CreateAssetMenu(fileName = "Surface Data", menuName = "Create Surface Data")]
    public class AeroSurfaceData : ScriptableObject
    {
        public float span = 0;
        public float chord = 0;
        public float flapPercent = 0;
        public float liftSlope = 1;
        public float zeroLiftAngle = 0;
        public float aoaStallNB = -30; //Negative stall angle of attack(Base)
        public float aoaStallPB = 30; //Positive stall angle of attack(Base)
        public float skinFrictionCoiffient = 1;
        public float aspectRatio = 0;

        public bool autoAR = false;

        public float AR
        {
            get
            {
                if(autoAR)
                    CalcucateAspectRatio();
                return aspectRatio;
            }
        }

        public float area
        {
            get
            {
                return span * chord;
            }
        }

        public float CalcucateAspectRatio()
        {
            aspectRatio = (span * span) / (span * chord);
            return aspectRatio;
        }
    }
}