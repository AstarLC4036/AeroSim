using System.Collections;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace AeroSim.AircraftMechanisms
{
    [ExecuteInEditMode]
    public class HingeAimIK : MonoBehaviour
    {
        public Transform fixedEnd;
        public Transform body;
        public Transform target;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (fixedEnd == null || target == null)
                return;

            Vector3 toTarget = target.position - fixedEnd.position;
            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            body.rotation = targetRotation;
        }
    }
}