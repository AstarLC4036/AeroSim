using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using System.Collections;
using UnityEngine;

namespace AeroSIm.Utils
{
    public class TestSAM : MonoBehaviour
    {
        public Missile missle;
        public RadarModule radar;
        public float launchTimer = 7;
        public float maxFireDelay = 7;
        public bool readyForLaunch = false;
        public float maxDst = 10000;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.L))
            {
                if (radar.ScannedAircrafts.Count > 0/* && radar.ScannedAircrafts.Exists(x => x == Aircraft.main)*/)
                {

                    if(radar.ScannedAircrafts[0] == Aircraft.main && radar.ScannedAircrafts.Count > 1)
                        radar.lockedAircraft = radar.ScannedAircrafts[1];
                    else if(radar.ScannedAircrafts.Count > 1)
                        radar.lockedAircraft = radar.ScannedAircrafts[0];
                    //radar.lockedAircraft = Aircraft.main;
                }

                if(launchTimer < 0)
                {
                    launchTimer = maxFireDelay;
                }
            }
        }

        private void FixedUpdate()
        {
            if (radar.lockedAircraft != null && Vector3.Distance(radar.lockedAircraft.transform.position, transform.position) < maxDst && !readyForLaunch)
            {
                launchTimer = maxFireDelay;
                readyForLaunch = true;
            }
            else if(readyForLaunch && Vector3.Distance(radar.lockedAircraft.transform.position, transform.position) > maxDst)
            {
                readyForLaunch = false;
            }

            if(readyForLaunch && launchTimer > 0)
            {
                launchTimer -= Time.fixedDeltaTime;
                if (launchTimer <= 0)
                {
                    GameObject newMsl = Instantiate(missle.gameObject);
                    newMsl.transform.parent = transform;
                    newMsl.transform.localPosition = Vector3.zero;
                    Missile msl = newMsl.GetComponent<Missile>();
                    newMsl.SetActive(true);
                    msl.DirectLock(radar.lockedAircraft.transform);
                    msl.Ignite();
                    radar.lockedAircraft = null;
                    readyForLaunch = false;
                }
            }
        }
    }
}