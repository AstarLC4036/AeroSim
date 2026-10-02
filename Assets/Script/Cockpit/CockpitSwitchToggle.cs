using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

namespace AeroSim.Cockpit
{
    public class CockpitSwitchToggle : CockpitInteractable
    {
        public Transform visualSwitch;
        public Collider pressTrigger;
        public Axis rotateAxis;
        public float rotateAngle = 180;
        public CockpitDataManager.InteractionEvent eventType;

        private bool toggled = false;

        private bool interacting = false;
        private Vector3 switchOffset;

        void Start()
        {
            switchOffset = visualSwitch.localEulerAngles;
        }

        public override void FixedUpdate()
        {
            if (interacting)
            {
                CockpitActionContext ctx = new CockpitActionContext(eventType, toggled ? 1 : 0);
                CockpitDataManager.onInteracion(ctx);
                interacting = false;
            }
        }

        public override void OnEnterInteraction()
        {
            interacting = true;

            toggled = !toggled;

            if (!toggled)
                visualSwitch.localEulerAngles = switchOffset;
            else
            {
                Vector3 dir = new Vector3(
                    rotateAxis == Axis.X ? 1 : 0,
                    rotateAxis == Axis.Y ? 1 : 0,
                    rotateAxis == Axis.Z ? 1 : 0
                ) * rotateAngle;
                visualSwitch.localEulerAngles -= dir;
            }
        }

        public override void OnQuitInteraction()
        {
            interacting = false;
        }
    }
}
