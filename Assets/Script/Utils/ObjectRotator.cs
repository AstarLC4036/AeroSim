using System.Collections;
using UnityEngine;

namespace AeroSim.Utils
{
    public class ObjectRotator : MonoBehaviour
    {
        public float rotateSpeed = 30;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            gameObject.transform.localEulerAngles = new Vector3(gameObject.transform.localEulerAngles.x, gameObject.transform.localEulerAngles.y + rotateSpeed * Time.deltaTime, gameObject.transform.localEulerAngles.z);
        }
    }
}