using System.Collections;
using UnityEngine;

namespace AeroSim.Utils
{
    public class InitalLauncher : MonoBehaviour
    {
        public Vector3 velocity;

        private Rigidbody rb;

        // Use this for initialization
        void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.AddForce((velocity.magnitude / Time.fixedDeltaTime * rb.mass) * velocity.normalized);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}