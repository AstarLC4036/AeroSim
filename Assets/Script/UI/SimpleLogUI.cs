using System.Collections;
using TMPro;
using UnityEngine;

namespace AeroSim.UI
{
    public class SimpleLogUI : MonoBehaviour
    {
        public TMP_Text logText;
        private float disappearTimer;
        public float disappearTime;
        
        public void DisplayLog(string message)
        {
            logText.text = message;
            disappearTimer = disappearTime;
        }

        private void Update()
        {
            if(disappearTimer > 0)
            {
                disappearTimer -= Time.deltaTime;
                if (disappearTimer <= 0)
                {
                    logText.text = "";
                }
            }
        }
    }
}