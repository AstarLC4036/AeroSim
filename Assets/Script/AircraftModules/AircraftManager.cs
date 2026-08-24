using AeroSim.AeroPhysics;
using System.Collections.Generic;
using UnityEngine;
using AeroSim.General;

namespace AeroSim.AircraftModules
{
    public class AircraftManager : MonoBehaviour
    {
        private static AircraftManager instance;
        public static AircraftManager Instance => instance;

        public List<Aircraft> aircrafts = new List<Aircraft>();
        public static List<Aircraft> Aircrafts => Instance.aircrafts;
        public List<Missile> missles = new List<Missile>();
        public static List<Missile> Missles => Instance.missles;
        public List<IRSource> irSources = new List<IRSource>();
        public static List<IRSource> IRSources => Instance.irSources;

        public Transform mslParent;

        void Awake()
        {
            instance = this;
        }

        void Start()
        {
            aircrafts.AddRange((Aircraft[])FindObjectsByType(typeof(Aircraft), FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID));
            irSources.AddRange((IRSource[])FindObjectsByType(typeof(IRSource), FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID));
        }

        // Update is called once per frame
        void Update()
        {
             
        }

        public static void RegistMSL(Missile msl)
        {
            Instance.missles.Add(msl);
            msl.transform.parent = Instance.mslParent;
        }
    }
}