using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using System.Threading;
using UnityEngine;

namespace AeroSim.Util
{
    public class CameraTrack : MonoBehaviour
    {
        public Transform target;
        public float deltaPos;
        public Vector3 relativePosition;
        public float movePercent = 0.7f;
        public float rotatePercent = 0.7f;
        public float rotateSensitivity = 1f;
        public bool lookAtVeloDir = false;
        public Aircraft aircraft;

        private Vector3 currentPos;
        private Vector3 lookPos;

        public void Start()
        {
            
        }

        public void FixedUpdate()
        {
            if (!Input.GetKey(Keybindings.holdControlInput))
                Track();
            else
            {
                transform.position = target.position + currentPos;
            }
        }

        public void Update()
        {
            if (Input.GetKey(Keybindings.holdControlInput))
                RotateAround();
        }

        void Track()
        {
            Vector3 targetPos = target.position + relativePosition.x * target.up + relativePosition.y * Vector3.up;
            Vector3 targetLookPos = lookAtVeloDir && aircraft.Velocity.magnitude > 10f ? transform.position + aircraft.Velocity.normalized * 100 : target.position + target.transform.up * deltaPos;

            lookPos = Vector3.Lerp(lookPos, targetLookPos, rotatePercent);
            if (Vector3.Distance(lookPos, target.position) < 0.1f)
            {
                lookPos = target.position;
            }

            transform.LookAt(lookPos);

            Vector3 pos = Vector3.Lerp(transform.position, targetPos, movePercent);
            transform.position = pos;

            if(Vector3.Distance(transform.position, targetPos) < 0.1f)
            {
                transform.position = targetPos;
            }

            currentPos = transform.position - target.position;
        }

        void RotateAround()
        {
            lookPos = Vector3.Lerp(lookPos, target.position, rotatePercent);
            if(Vector3.Distance(lookPos, target.position) < 0.1f)
            {
                lookPos = target.position;
            }

            transform.LookAt(lookPos, transform.up);

            float deltaX = Input.mousePositionDelta.x * rotateSensitivity;
            float deltaY = Input.mousePositionDelta.y * rotateSensitivity;

            currentPos = RotateRound(currentPos, Vector3.zero, transform.up, deltaX);
            currentPos = RotateRound(currentPos, Vector3.zero, transform.right, -deltaY);
        }

        public static Vector3 RotateRound(Vector3 position, Vector3 center, Vector3 axis, float angle)
        {
            return Quaternion.AngleAxis(angle, axis) * (position - center) + center;
        }
    }
}