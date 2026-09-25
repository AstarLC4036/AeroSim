using AeroSim.AeroPhysics;
using AeroSim.Cockpit;
using AeroSim.General;
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
                if (currentMissle.lockState == Missile.MissileState.None)
                {
                    if(ActiveSeeker())
                        VehicleLog.LogMsg($"{currentMissle.nameId} > 导引头启动");
                }
                else if (currentMissle.lockState == Missile.MissileState.Locked)
                    Fire();
            }
            else if(Keybindings.shutdownSeekerDown && parentAircraft.isControlling)
            {
                if (currentMissle.lockState != Missile.MissileState.None)
                {
                    currentMissle.ShutdownSeeker();
                    VehicleLog.LogMsg($"{currentMissle.nameId} > 导引头关机");
                }
            }

            if (Input.GetKeyDown(Keybindings.mslView) && parentAircraft.isControlling)
            {
                if(CameraController.Instance.target != transform)
                {
                    CameraController.Instance.target = transform;
                    CameraController.Instance.directlyRotate = false;
                }
                else if(launchedMissle != null)
                {
                    CameraController.Instance.target = launchedMissle.transform;
                    CameraController.Instance.directlyRotate = true;
                }
            }
        }

        private void FixedUpdate()
        {
            //Update weapon state
            if (parentAircraft.isControlling)
            {
                if (currentMissle != null)
                {
                    if (currentMissle.lockState == Missile.MissileState.Locking)
                    {
                        if (CockpitDataManager.Instance.weaponState != CockpitDataManager.WeaponState.Lock)
                            CockpitDataManager.Instance.weaponState = CockpitDataManager.WeaponState.Lock;
                    }
                    else if (currentMissle.lockState == Missile.MissileState.Locked)
                    {
                        if(CockpitDataManager.Instance.weaponState != CockpitDataManager.WeaponState.InRange && CockpitDataManager.Instance.weaponState != CockpitDataManager.WeaponState.Shoot)
                            CockpitDataManager.Instance.weaponState = CockpitDataManager.WeaponState.InRange;
                    }
                    else if (CockpitDataManager.Instance.weaponState != CockpitDataManager.WeaponState.WeaponReay)
                    {
                        CockpitDataManager.Instance.weaponState = CockpitDataManager.WeaponState.WeaponReay;
                    }
                }
                else if (CockpitDataManager.Instance.weaponState != CockpitDataManager.WeaponState.NoWeapon)
                {
                    CockpitDataManager.Instance.weaponState = CockpitDataManager.WeaponState.NoWeapon;
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

        public bool ActiveSeeker()
        {
            if (currentMissle.type == Missile.MissileType.IR)
            {
                currentMissle.ActiveSeeker();
                return true;
            }
            else if (currentMissle.type == Missile.MissileType.Active && target != null)
            {
                currentMissle.ActiveSeeker();
                currentMissle.SetTarget(target);

                if (currentMissle.hasDatalink && parentAircraft != null && parentAircraft.datalink != null)
                {
                    parentAircraft.datalink.RegisterDatalink(currentMissle, target);
                }
                return true;
            }
            else
                return false;
        }

        public void Fire()
        {
            if (currentMissle.lockState != Missile.MissileState.Locked)
            {
                return;
            }

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