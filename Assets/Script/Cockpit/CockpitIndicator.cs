using System.Collections;
using UnityEngine;

namespace AeroSim.Cockpit
{
    public class CockpitIndicator : MonoBehaviour
    {
        void FixedUpdate()
        {
            UpdateIndicator();
        }

        protected virtual void UpdateIndicator()
        {

        }
    }
}