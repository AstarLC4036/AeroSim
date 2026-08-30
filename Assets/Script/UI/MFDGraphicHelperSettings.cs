using System.Collections;
using UnityEngine;

namespace AeroSim.UI
{
    public class MFDGraphicHelperSettings : MonoBehaviour
    {
        private static MFDGraphicHelperSettings instance;
        public static MFDGraphicHelperSettings Instance => instance;
        public ComputeShader mfdShader;
        public static ComputeShader MfdShader => instance.mfdShader;

        void Awake()
        {
            instance = this;
            //MfdShader = mfdShader;
        }
    }
}