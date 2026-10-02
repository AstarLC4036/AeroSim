using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.AircraftModules.Power
{
    public class ElectricalModule : AircraftModule
    {
        [Serializable]
        public abstract class PowerDevice
        {
            //public PowerBus bus;
            public string deviceId;
            public bool switchOn;
            public bool breakerClosed;

            [NonSerialized]
            public float voltage;
            [NonSerialized]
            public float current;

            public bool Available => switchOn && breakerClosed;

            public virtual void BindBus(PowerBus bus)
            {
                // well, nothing use this
                //this.bus = bus;
            }
        }

        [Serializable]
        public class PowerBus
        {
            public string busId;
            public List<PowerSource> sources;
            public List<PowerPort> ports;
            public float smoothing;

            [NonSerialized]
            public bool isOverloaded;
            [NonSerialized]
            public float voltage;

            public void UpdateDevices()
            {
                foreach (var device in sources)
                {
                    device.BindBus(this);
                }
                foreach (var device in ports)
                {
                    device.BindBus(this);
                }
            }

            public void UpdateBus()
            {
                float pDemand = 0;
                foreach (var port in ports)
                {
                    if (!port.Available)
                        continue;

                    pDemand += port.demandPower;
                }

                float iCap = 0;
                float g = 0;
                foreach (var source in sources)
                {
                    if (!source.Available)
                        continue;

                    g += 1 / Mathf.Max(source.resistance, 1e-4f);
                    iCap += source.maxCurrent;
                }

                if (g <= 0)
                {
                    ApplyOutput(0, 0);
                    return;
                }

                float e_eq = 0;
                float r_eq = 1 / g;
                foreach (var source in sources)
                {
                    if (!source.Available)
                        continue;

                    e_eq += source.emf / Mathf.Max(source.resistance, 1e-4f);
                }
                e_eq /= g;

                //TODO: Shutdown low necessay modules to keep core module always available

                // old simple method
                //float pCap = e_eq * e_eq / (4f * r_eq);
                //sumPayloadPower = Mathf.Min(sumPayloadPower, pCap); // make sure that we can always work out voltage, but it will drops

                // U^2 - E_eq * U + R_eq * P_load = 0
                float disc = e_eq * e_eq - 4 * r_eq * pDemand;
                float u_result = disc >= 0 ? (e_eq + Mathf.Sqrt(Mathf.Max(disc, 0))) / 2 : e_eq * 0.5f;

                if (iCap > 0f && (e_eq - u_result) / r_eq > iCap)
                    u_result = Mathf.Max(e_eq - iCap * r_eq, 0);

                float pSupply = u_result * (e_eq - u_result) / r_eq;
                float scale = pDemand > 1e-4f ? Mathf.Clamp01(pSupply / pDemand) : 1f;

                isOverloaded = scale < 0.999f;
                voltage = smoothing > 0 ? Mathf.Lerp(voltage, u_result, smoothing) : u_result;

                ApplyOutput(u_result, scale);
            }

            void ApplyOutput(float u_result, float scale)
            {
                bool live = u_result > 0.01f;

                foreach (var port in ports)
                {
                    if (!port.Available || !live)
                    {
                        port.current = 0;
                        port.voltage = 0;
                        continue;
                    }

                    port.current = (port.demandPower * scale) / u_result; // 'port.demandPower * scale' -> source is overloaded, it can't provide enough power.
                    port.voltage = u_result - port.current * port.wireResistance;
                }

                foreach (var source in sources)
                {
                    if (!source.Available || !live)
                    {
                        source.current = 0;
                        source.voltage = 0;
                        continue;
                    }

                    source.current = (source.emf - u_result) / Mathf.Max(source.resistance, 1e-4f);
                    source.voltage = u_result;
                }
            }

            public PowerSource GetSource(string deviceId)
            {
                return sources?.Find(source => source.deviceId == deviceId);
            }

            public PowerPort GetPort(string deviceId)
            {
                return ports?.Find(port => port.deviceId == deviceId);
            }
        }

        [Serializable]
        public class PowerSource : PowerDevice
        {
            public float emf;
            public float resistance;
            public float maxCurrent;
            public bool isRegulated;
        }

        [Serializable]
        public class PowerPort : PowerDevice
        {
            public float demandPower;
            public float wireResistance;
            public bool Powered => switchOn && breakerClosed && voltage > 0 && current > 0;
        }

        public PowerBus mainBus;

        private void Start()
        {
            mainBus?.UpdateDevices();
        }

        private void FixedUpdate()
        {
            mainBus?.UpdateBus();
        }
    }
}