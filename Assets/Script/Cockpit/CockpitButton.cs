using System.Collections;
using UnityEngine;
using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using System.Threading;
using UnityEngine.Animations;

namespace AeroSim.Cockpit
{
    public class CockpitButton : CockpitInteractable
    {
        [Header("Interaction")]
        public float triggerTime;
        public CockpitDataManager.InteractionEvent eventType;
        public int eventState;
        [Header("Visual")]
        public Transform visualButton;
        public Collider pressTrigger;
        public Axis pushAxis;
        public float pushDst;
        [Header("Audio")]
        public bool playSoundEffect = false;

        private float triggerTimer;
        private bool interacting = false;
        private Vector3 btnOffset;

        void Start()
        {
            btnOffset = visualButton.localPosition;
        }

        public override void FixedUpdate()
        {
            if(interacting)
            {
                if (triggerTimer < triggerTime)
                {
                    triggerTimer += Time.fixedDeltaTime;
                }
                else
                {
                    CockpitActionContext ctx = new CockpitActionContext(eventType, eventState);
                    CockpitDataManager.onInteracion(ctx);
                    triggerTimer = 0;
                    interacting = false;
                }
            }
        }

        public override void OnEnterInteraction()
        {
            interacting = true;
            triggerTimer = 0;

            Vector3 dir = new Vector3(
                pushAxis == Axis.X ? 1 : 0,
                pushAxis == Axis.Y ? 1 : 0,
                pushAxis == Axis.Z ? 1 : 0
            ) * pushDst;
            visualButton.localPosition -= dir;
        }

        public override void OnQuitInteraction()
        {
            interacting = false;
            triggerTimer = 0;
            visualButton.localPosition = btnOffset;
        }
    }
}