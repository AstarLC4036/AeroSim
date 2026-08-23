using AeroSim.AeroPhysics;
using AeroSim.Utils;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AeroSim.UI
{
    public class AircraftUI : MonoBehaviour
    {
        private static AircraftUI instance;
        public static AircraftUI Instance => instance;

        public GameObject targetingPodView;
        public TMP_Text throttleText;
        public TMP_Text altText;
        public TMP_Text spdText;
        public TMP_Text overloadText;
        public Aircraft aircraft;
        private Rigidbody aircraftRB;

        public RectTransform rwr;
        public static RectTransform RWR => Instance.rwr;
        public TMP_Text rwrMsg;
        public static TMP_Text RwrMsg => Instance.rwrMsg;

        public GameObject rwrMsgBase;

        public Font rwrFont;
        public static Font RwrFont => Instance.rwrFont;

        public Texture2D rwrAlarmRing;
        public static Texture2D RwrAlarmRing => Instance.rwrAlarmRing;

        public Material lineMaterial;
        public static Material LineMaterial => Instance.lineMaterial;
        public static bool isTargetingViewEnabled => Instance.targetingPodView.activeSelf;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            aircraft = Aircraft.main;
            aircraftRB = aircraft.GetComponent<Rigidbody>();
        }

        private void Update()
        {
            int throttlePercent = (int)(aircraft.engine.thurst / aircraft.engine.maxThurst * 100);
            throttleText.text = throttlePercent <= 100 ? $"{throttlePercent}%" : $"{throttlePercent}% <color=red>[加力]</color>";
            altText.text = $"{((int)((aircraft.transform.position.y - OriginKeeper.origin.y) * 100)) / 100}m";
            spdText.text = $"{((int)(aircraftRB.velocity.magnitude / 1000 * 3600 * 100)) / 100}km/h";
            overloadText.text = $"{(int)(aircraft.G * 10) / 10f} G";
        }

        public void DisplayRWRMsgBG(bool display = true)
        {
            rwrMsgBase.SetActive(display);
            rwrMsg.gameObject.SetActive(display);
        }

        public static void DisplayRWRMsgBGS(bool display = true)
        {
            Instance.DisplayRWRMsgBG(display);
        }

        public static void DisplayTargetingPodView(bool display = true)
        {
            Instance.targetingPodView.SetActive(display);
        }

        public static bool IsRWRLabelActived()
        {
            return Instance.rwrMsgBase.activeSelf;
        }

        void OnGUI()
        {

        }
    }
}