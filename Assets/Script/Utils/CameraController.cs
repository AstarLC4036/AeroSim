using AeroSim.AeroPhysics;
using AeroSim.Render;
using AeroSim.Utils;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static AeroSim.Utility.MathUtility;

namespace AeroSim.InputSystem
{
    public class CameraController : MonoBehaviour
    {
        private static CameraController instance;
        public static CameraController Instance => instance;

        [Serializable]
        public struct CameraView
        {
            public Vector3 offset;
            public Vector3 deltaPos;
            public bool followRotation;
            public bool enableIRView;
            public bool enableStable;

            public CameraView(Vector3 offset, Vector3 deltaPos, bool followRotation, bool enableIRView, bool enableStable)
            {
                this.offset = offset;
                this.deltaPos = deltaPos;
                this.followRotation = followRotation;
                this.enableIRView = enableIRView;
                this.enableStable = enableStable;
            }
        }

        public CameraView[] views;

        public float sensitivity = 1;
        public float aimLerpParam = 0.1f;
        public Vector3 offset = Vector3.zero;
        public Vector3 deltaPos = new Vector3(0, 0, 0);
        public bool followRotation = false;
        public bool enableIRView = false;
        public bool enableStable = false;
        public Transform target;

        public Volume mainVolume;
        private IREffect IREffect;

        public float defaultFov = 60;
        public float focusFov = 30;
        public bool isFocusing = false;

        private float avgMouseDelta = 0;
        private float mouseDeltaTimer = 0;
        public float calcAvgTime = 0.5f;
        private bool isMoveingView = false;
        private Vector3 fwd = new Vector3(0,0,1);
        private int currentViewIndex = 0;
        private Vector3 trackPoint;
        private bool isStableTargetAvaliable = false;

        private void Awake()
        {
            instance = this;
        }

        // Use this for initialization
        void Start()
        {
            if (views.Length > 0)
                SetView(0);
            else
                Debug.LogWarning($"There's no available view for camera \"{gameObject.name}\"");

            if(!mainVolume.profile.TryGet<IREffect>(out IREffect))
            {
                Debug.LogWarning($"'IR Effect' is not avaliable, we can't find it in the volume profile.");
            }

            OriginKeeper.onOriginChange += OnOriginChange;
        }

        private void OnOriginChange(Vector3 delta)
        {
            if(enableStable && isStableTargetAvaliable)
            {
                trackPoint += delta;
            }
        }

        public void SetView(int viewIndex)
        {
            CameraView view = views[viewIndex];
            offset = view.offset;
            deltaPos = view.deltaPos;
            followRotation = view.followRotation;
            enableIRView = view.enableIRView;
            enableStable = view.enableStable;
            currentViewIndex = viewIndex;

            isStableTargetAvaliable = false;

            if(enableIRView && IREffect != null)
            {
                IREffect.enabled.value = true;
            }
            if(!enableIRView && IREffect != null)
            {
                IREffect.enabled.value = false;
            }
        }

        void Update()
        {
            if (Input.GetKey(Keybindings.holdControlInput) || enableStable)
            {
                Vector3 mouseDelta = Input.mousePositionDelta;

                avgMouseDelta += mouseDelta.magnitude;
                mouseDeltaTimer += Time.deltaTime;

                if (mouseDeltaTimer >= calcAvgTime)
                {
                    avgMouseDelta /= mouseDeltaTimer;
                    float mouseMoveMagnitude = avgMouseDelta;
                    avgMouseDelta = 0;
                    mouseDeltaTimer = 0;

                    //if (mouseMoveMagnitude > 0.1f && !isMoveingView)
                    //{
                    //    isMoveingView = true;
                    //}
                    if (mouseMoveMagnitude <= 0.1f && isMoveingView)
                    {
                        isMoveingView = false;

                        if (enableStable)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(transform.position, transform.forward, out hit, 40 * 1000))
                            {
                                trackPoint = hit.point;
                                isStableTargetAvaliable = true;
                            }
                            else
                            {
                                trackPoint = Vector3.zero;
                                isStableTargetAvaliable = false;
                            }
                        }
                    }
                }

                if(mouseDelta.magnitude > 0.5f && !isMoveingView)
                {
                    isMoveingView = true;
                }

                //fwd = transform.forward;

                fwd = RotateRound(fwd, Vector3.zero, transform.up, mouseDelta.x * sensitivity * Time.deltaTime);
                fwd = RotateRound(fwd, Vector3.zero, transform.right, -mouseDelta.y * sensitivity * Time.deltaTime);
            }

            if (Input.GetKeyDown(Keybindings.changeView))
            {
                if (currentViewIndex + 1 < views.Length)
                {
                    SetView(currentViewIndex + 1);
                }
                else
                {
                    SetView(0);
                }
            }

            if (Input.GetKeyDown(Keybindings.focusCam))
            {
                isFocusing = true;
            }
            else if(Input.GetKeyUp(Keybindings.focusCam))
            {
                isFocusing = false;
            }

            if(isFocusing)
            {
                Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, focusFov, 0.5f);
                if(Mathf.Abs(Camera.main.fieldOfView - focusFov) < 0.1f)
                {
                    Camera.main.fieldOfView = focusFov;
                }
            }
            else
            {
                Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, defaultFov, 0.5f);
                if (Mathf.Abs(Camera.main.fieldOfView - defaultFov) < 0.1f)
                {
                    Camera.main.fieldOfView = defaultFov;
                }
            }
        }

        void LateUpdate()
        {
            transform.position = target.position + deltaPos.z * fwd + Vector3.up * deltaPos.y + target.forward * offset.z + target.right * offset.x + target.up * offset.y;

            if (!Input.GetKey(Keybindings.holdControlInput) && !enableStable)
            {
                fwd = Vector3.Lerp(fwd, Aircraft.main.targetDir, aimLerpParam * Time.fixedDeltaTime);
            }
            else if (enableStable && isStableTargetAvaliable && !isMoveingView)
            {
                fwd = (trackPoint - transform.position).normalized;
            }

            transform.LookAt(transform.position + fwd, followRotation ? target.up : Vector3.up);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(target.position + offset, 0.5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(target.position + offset + new Vector3(0, deltaPos.y, deltaPos.x), 0.5f);
        }
    }
}