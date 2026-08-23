using AeroSim.Util;
using AeroSim.InputSystem;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AeroSim.AircraftModules;
using AeroSim.UI;

namespace AeroSim.AeroPhysics
{
    public class Aircraft : MonoBehaviour
    {
        public static Aircraft main;

        public AeroSurface[] surfaces;

        [Header("Config")]
        public string aircraftName = "Aircraft";
        public string simpifiedName = "CFT";
        public Vector3 centerOfMass;
        public bool isControlling = false;

        public float liftMultiply = 1;
        public float dragMultiply = 1;
        public float torqueMultiply = 1;

        //[Header("Thruster")]
        //public float maxThurst = 10000;
        //public float thurst = 0;
        //public float deltaThrust = 100;

        // aero sim
        [SerializeField]
        private Vector3 velocity;
        private Vector3 localFlow;
        private float angleOfAttack;

        private float gOverload = 1;

        // nav
        public Vector3 targetDir = Vector3.forward;
        private Vector3 controllingInput;
        private Vector3 actualInput;

        [Header("Modules")]
        public EngineModule engine;
        public RadarModule radar;
        public DatalinkModule datalink;
        public TargetingPodModule targetingPod;
        public RWRModule rwr;
        public MAWModule maw;
        public MissileModule mslManager;
        public FlightController flightController;
        public MFDDisplays mfdDisplay;

        // public fields
        public float AOA => angleOfAttack;
        public Vector3 Velocity => velocity;
        public float G => gOverload;
        public Vector3 ControllingInput => controllingInput;

        // private fields
        private Rigidbody rb;
        private Vector3 localCenterOfMass;

        public Rigidbody Rb => rb;

        public void Awake()
        {
            if(isControlling)
                main = this;
        }

        public void Start()
        {
            //Register all aero surfaces
            surfaces = GetComponentsInChildren<AeroSurface>(false);
            foreach (AeroSurface surface in surfaces)
            {
                surface.parent = this;
            }

            rb = GetComponent<Rigidbody>();

            //use 'try' block instead
            //Init modules
            radar = GetComponent<RadarModule>();
            radar.Init(this);
            datalink = GetComponent<DatalinkModule>();
            datalink.Init(this);
            if(targetingPod != null)
            targetingPod.Init(this);
            rwr = GetComponent<RWRModule>();
            rwr.Init(this);
            maw = GetComponent<MAWModule>();
            maw.Init(this);
            mslManager = GetComponent<MissileModule>();
            mslManager.Init(this);
            engine = GetComponent<EngineModule>();
            engine.Init(this);
            flightController = GetComponent<FlightController>();
            flightController.Init(this);
            mfdDisplay = GetComponent<MFDDisplays>();
            mfdDisplay.Init(this);

            if (this == main)
                MainAircraftInit();

            if(radar != null && mslManager != null)
            {
                radar.onLock += (aircraft) => { if (aircraft != null) mslManager.target = aircraft.transform; else mslManager.target = null; };
            }
        }

        public void Update()
        {
             
        }

        public void FixedUpdate()
        {
            CalcucateState();
            UpdateForces();
            UpdateInput(Time.fixedDeltaTime);
        }

        void MainAircraftInit()
        {
            HUDDrawer.Instance.SetRadar(radar);
            RadarHUDDrawer.SetAircraft(this);
            ThreatDrawer.GlobalInit(this);
        }

        void CalcucateState()
        {
            velocity = rb.velocity;
            localFlow = transform.InverseTransformVector(velocity);
            angleOfAttack = Mathf.Atan2(-localFlow.y, localFlow.z) * Mathf.Rad2Deg;
            localCenterOfMass = centerOfMass.x * transform.forward + centerOfMass.y * transform.up + centerOfMass.z * transform.right;

            //liftCoiffient = liftCurve.Evaluate(angleOfAttack);
            //dragCoiffient = dragCurve.Evaluate(angleOfAttack);
            //torqueCoiffient = torqueCurve.Evaluate(angleOfAttack);
            //Debug.DrawLine(transform.position, transform.position + localFlow, Color.blue);
        }

        public void UpdateForces()
        {
            UpdateAeroForces();
        }

        public void UpdateAeroForces()
        {
            BiVector3 forcesAndTorque = new BiVector3();
            foreach (AeroSurface surface in surfaces)
            {
                BiVector3 forces = surface.CalcucateForces(localCenterOfMass);
                forcesAndTorque += forces;
                Debug.DrawLine(surface.transform.position, surface.transform.position + surface.LocalVelocity / 10, Color.white);
                Debug.DrawLine(surface.transform.position, surface.transform.position + forces.lift / 1000, Color.green);
                Debug.DrawLine(surface.transform.position, surface.transform.position + forces.drag / 1000, Color.red);
                Debug.DrawLine(surface.transform.position, surface.transform.position + forces.torque / 1000, Color.blue);
            }

            rb.AddForce(forcesAndTorque.lift * liftMultiply);
            rb.AddForce(forcesAndTorque.drag * dragMultiply);
            rb.AddTorque(forcesAndTorque.torque * torqueMultiply);

            gOverload = forcesAndTorque.lift.magnitude * liftMultiply / (rb.mass * Physics.gravity.magnitude);
        }

        public void UpdateInput(float dt)
        {
            controllingInput = flightController.AimRingControl(targetDir, dt);
            float clampedX = Mathf.Clamp(controllingInput.x, -1, 1);
            float clampedY = Mathf.Clamp(controllingInput.y, -1, 1);
            float clampedZ = Mathf.Clamp(controllingInput.z, -1, 1);
            controllingInput = new Vector3(clampedX, clampedY, clampedZ);

            float speedFactor = Mathf.Clamp(165 / Mathf.Max(velocity.magnitude, 1f), 0.2f, 1.0f); // 165 -> reference speed
            actualInput = controllingInput * speedFactor;

            foreach (AeroSurface surface in surfaces)
            {
                surface.UpdateInput(actualInput);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position + localCenterOfMass, 0.5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + targetDir.normalized * 10);
        }
    }
}