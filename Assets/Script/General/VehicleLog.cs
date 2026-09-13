using AeroSim.UI;
using System.Collections;
using UnityEngine;

namespace AeroSim.General
{
    public class VehicleLog : MonoBehaviour
    {
        private static VehicleLog instance;
        public static VehicleLog Instance => instance;
        public SimpleLogUI logUI;

        private void Awake()
        {
            instance = this;
        }

        public void Log(string message)
        {
            logUI.DisplayLog(message);
        }

        public static void LogMsg(string message)
        {
            if (instance != null)
            {
                instance.Log(message);
            }
        }
    }
}