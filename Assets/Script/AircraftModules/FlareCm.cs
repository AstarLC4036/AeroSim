using AeroSim.General;
using System.Collections;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class FlareCm : MonoBehaviour
    {
        public IRSource irSrc;

        public float burnTime = 12;
        private float burnTimer;
        public Rigidbody rb;

        public void Reset()
        {
            burnTimer = burnTime;
            irSrc.enabled = true;
        }

        public void FixedUpdate()
        {
            if (burnTimer > 0)
            {
                burnTimer -= Time.fixedDeltaTime;
                if (burnTimer <= 0)
                {
                    irSrc.enabled = false;
                    CountermeasurePool.Instance.flarePool.Release(this);
                }
            }
        }
    }
}