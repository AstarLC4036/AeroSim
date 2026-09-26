using AeroSim.AeroPhysics;
using AeroSim.Utils;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
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
        public TMP_Text tasText;
        public TMP_Text iasText;
        public TMP_Text machSpdText;
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
            // 真实高度：和 Aircraft / AtmosphereEnv 用同一个口径
            float altitude = aircraft.transform.position.y - FloatingOrigin.origin.y;
            float sonic = Mathf.Max(AtmosphereEnv.SonicSpeed(altitude), 1f);

            // 节流阀显示「杆位 + 卡位名 + N1」：杆位是飞行员给的，N1 是发动机的响应
            int leverPercent = Mathf.RoundToInt(aircraft.engine.ThrottlePercent);
            int n1Percent = Mathf.RoundToInt(aircraft.engine.N1Percent);
            float ias = aircraftRB.velocity.magnitude * Mathf.Sqrt(AtmosphereEnv.Density(altitude) / 1.225f);
            string abTag = aircraft.engine.IsAfterburner ? " <size=16><color=red>[加力]</color></size>" : string.Empty;
            throttleText.text = $"{aircraft.engine.DetentName} {leverPercent} <size=16>% · N1 {n1Percent}%</size>{abTag}";
            altText.text = $"{((int)(altitude * 100)) / 100} <size=16>m</size>";
            tasText.text = $"{((int)(aircraftRB.velocity.magnitude / 1000 * 3600 * 100)) / 100} <size=16>km/h</size>";
            iasText.text = $"{((int)(ias / 1000 * 3600 * 100)) / 100} <size=16>km/h</size>";
            machSpdText.text = $"{(int)(aircraftRB.velocity.magnitude / sonic * 100) / 100f} <size=16>Mach</size>";
            overloadText.text = $"{(int)(aircraft.G * 10) / 10f} <size=16>G</size>";
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