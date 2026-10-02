// Power/PowerLink.cs —— 运行期与编辑期共用的「用电申请单」
using System;
using UnityEngine;

namespace AeroSim.AircraftModules.Power
{
    [Serializable]
    public class PowerLink
    {
        public string portId;
        public string label; // Display name for UI

        public ElectricalModule module;
        public ElectricalModule.PowerBus attachedBus;
        public ElectricalModule.PowerPort attachedPort;
    }
}