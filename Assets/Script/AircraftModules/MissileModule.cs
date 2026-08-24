using AeroSim.AeroPhysics;
using AeroSim.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    public class MissileModule : AircraftModule
    {
        public List<Missile> leftMSLs = new List<Missile>();
        public List<Missile> rightMSLs = new List<Missile>();
        public Missile currentMissle;
        public Missile launchedMissle;
        public Transform target = null;

        private bool currentSideFlag = false; //false => left; true => right

        // Use this for initialization
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(Keybindings.fireMain) && parentAircraft.isControlling)
            {
                if (currentMissle.lockState == Missile.LockState.None)
                    ActiveSeeker();
                else if (currentMissle.lockState == Missile.LockState.Locked)
                    Fire();
            }

            if(Input.GetKeyDown(Keybindings.mslView) && parentAircraft.isControlling)
            {
                if(CameraController.Instance.target != transform)
                {
                    CameraController.Instance.target = transform;
                }
                else if(launchedMissle != null)
                {
                    CameraController.Instance.target = launchedMissle.transform;
                }
            }
        }

        public override void Init(Aircraft parentAircraft)
        {
            base.Init(parentAircraft);

            foreach(Missile msl in leftMSLs)
            {
                msl.parentAircraft = parentAircraft;
            }
            foreach(Missile msl in rightMSLs)
            {
                msl.parentAircraft = parentAircraft;
            }

            SwitchSide();
        }

        public void SwitchSide()
        {
            if (currentSideFlag)
            {
                if (rightMSLs.Count <= 0)
                {
                    currentMissle = null;
                }
                else
                {
                    currentMissle = rightMSLs[0];
                    return;
                }
            }

            if (!currentSideFlag)
            {
                if (leftMSLs.Count <= 0)
                {
                    currentMissle = null;
                }
                else
                {
                    currentMissle = leftMSLs[0];
                    return;
                }
            }
        }

        public void ActiveSeeker()
        {
            if (currentMissle.type == Missile.MissileType.IR)
            {
                currentMissle.ActiveSeeker();
            }
            else if (currentMissle.type == Missile.MissileType.Active && target != null)
            {
                currentMissle.ActiveSeeker();
                currentMissle.SetTarget(target);
            }
        }

        public void Fire()
        {
            if (currentMissle.type == Missile.MissileType.Active && target == null)
                return;

            if (currentMissle != null)
            {
                currentMissle.Ignite();
                launchedMissle = currentMissle;
                if (currentSideFlag)
                {
                    rightMSLs.Remove(currentMissle);
                }
                else
                {
                    leftMSLs.Remove(currentMissle);
                }

                currentSideFlag = !currentSideFlag;
                SwitchSide();
            }
        }
    }
}