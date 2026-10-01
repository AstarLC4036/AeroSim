using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using System.Collections;
using UnityEngine;

namespace AeroSim.Cockpit
{
    public class CockpitInteractionManager : MonoBehaviour
    {
        public LayerMask triggerLayer;
        public float maxDst;

        private bool interacting = false;
        private CockpitInteractable interacionTarget;

        public void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = new Ray(Camera.main.transform.position, CameraController.Instance.enableMoveCameraDirectly ? Camera.main.transform.forward : Aircraft.main.targetDir);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, maxDst, triggerLayer))
                {
                    interacting = true;
                    interacionTarget = hit.collider.transform.GetComponent<CockpitInteractable>();
                    interacionTarget.OnEnterInteraction();
                }
            }
            else if(Input.GetMouseButtonUp(0) && interacting)
            {
                interacting = false;
                interacionTarget.OnQuitInteraction();
                interacionTarget = null;
            }
        }
    }
}