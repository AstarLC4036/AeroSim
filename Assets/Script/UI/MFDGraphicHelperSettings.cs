using System.Collections;
using TMPro;
using UnityEngine;

namespace AeroSim.UI
{
    public class MFDGraphicHelperSettings : MonoBehaviour
    {
        private static MFDGraphicHelperSettings instance;
        public static MFDGraphicHelperSettings Instance => instance;
        public ComputeShader mfdShader;
        public static ComputeShader MfdShader => instance.mfdShader;

        [Header("文字")]
        [Tooltip("MFD 默认字体（TMP Font Asset / SDF）。也可以逐 MFD 用 MFDGraphicHelper.TextFont 覆盖。")]
        public TMP_FontAsset defaultFont;
        [Range(0.01f, 0.5f)] public float defaultTextSharpness = 0.1f;

        void Awake()
        {
            instance = this;
            //MfdShader = mfdShader;
        }
    }
}