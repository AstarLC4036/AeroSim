using AeroSim.Cockpit;
using System;
using System.Collections.Generic;
using System.Text;

namespace AeroSim.AircraftModules
{
    public class CockpitInteractionHandler : AircraftModule
    {
        public void Start()
        {
            CockpitDataManager.onInteracion += HandleInteracionEvent;
        }

        public void OnApplicationQuit()
        {
            CockpitDataManager.onInteracion -= HandleInteracionEvent;
        }

        public void OnDestroy()
        {
            CockpitDataManager.onInteracion -= HandleInteracionEvent;
        }

        public void HandleInteracionEvent(CockpitActionContext ctx)
        {
            switch(ctx.actionType)
            {
                case CockpitDataManager.InteractionEvent.StartEngine:
                    parentAircraft.engine.StartEngine();
                    break;
                case CockpitDataManager.InteractionEvent.ToggleEngine:
                    ToggleEngine(ctx.actionState);
                    break;
                case CockpitDataManager.InteractionEvent.Avionics_SetMode:
                    ToggleAvionics(ctx.actionState);
                    break;
                case CockpitDataManager.InteractionEvent.ToggleBattery:
                    ToggleBattery(ctx.actionState);
                    break;
                case CockpitDataManager.InteractionEvent.ToggleGenerator_AC:
                    ToggleACGen(ctx.actionState);
                    break;
                case CockpitDataManager.InteractionEvent.ToggleGenerator_DC:
                    ToggleDCGen(ctx.actionState);
                    break;
            }
        }

        public void ToggleEngine(int state)
        {
            bool toggled = state != 0;
            parentAircraft.engine.isEngineToggled = toggled;
        }

        public void ToggleAvionics(int state)
        {
            if (state == 0)
            {
                parentAircraft.electrical.mainBus.GetPort("AVN-4").switchOn = false;
            }
            else if (state == 1 || state == 2)
            {
                parentAircraft.electrical.mainBus.GetPort("AVN-4").switchOn = true;
            }
        }

        public void ToggleBattery(int state)
        {
            bool switchOn = state != 0;

            parentAircraft.electrical.mainBus.GetSource("BAT").switchOn = switchOn;
        }

        public void ToggleACGen(int state)
        {
            bool switchOn = state != 0;

            parentAircraft.electrical.mainBus.GetSource("GEN-AC").switchOn = switchOn;
        }

        public void ToggleDCGen(int state)
        {
            bool switchOn = state != 0;

            parentAircraft.electrical.mainBus.GetSource("GEN-DC").switchOn = switchOn;
        }
    }
}
