using AeroSim.AeroPhysics;
using System.Collections;
using UnityEngine;

namespace Assets.Script.AeroPhysics
{
    public class AeroCenterCaculator : MonoBehaviour
    {
        public Aircraft aircraft;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        Vector3 CalculateAerodynamicCenter()
        {
            Vector3 weightedSum = Vector3.zero;
            float totalWeight = 0;

            foreach (var s in aircraft.surfaces)
            {
                float weight = s.liftSlope * s.area;
                weightedSum += s.transform.position * weight;
                totalWeight += weight;
            }

            if (totalWeight == 0) return transform.position;
            return weightedSum / totalWeight;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Vector3 ac = CalculateAerodynamicCenter();
            Gizmos.DrawSphere(ac, 0.1f);
        }
    }
}