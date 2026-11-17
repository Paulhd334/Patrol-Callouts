using System;
using Rage;
using Rage.Native;

namespace PatrolsCallouts.Utils
{
    internal static class CalloutUtils
    {
        internal static bool HasCollision(Entity entity)
        {
            if (entity == null || !entity.Exists()) return false;

            try
            {
                return NativeFunction.CallByName<bool>("HAS_COLLISION_LOADED_AROUND_ENTITY", entity);
            }
            catch (Exception ex)
            {
                Main.Log("HAS_COLLISION_LOADED_AROUND_ENTITY failed: " + ex.Message);
                return false;
            }
        }

        internal static void SetHazards(Vehicle vehicle, bool enabled)
        {
            if (vehicle == null || !vehicle.Exists()) return;

            try
            {
                // GTA V has no SET_VEHICLE_HAZARD(S)_LIGHTS native. Hazard
                // lights are its left and right indicators, both enabled.
                NativeFunction.CallByName<uint>("SET_VEHICLE_INDICATOR_LIGHTS", vehicle, 0, enabled);
                NativeFunction.CallByName<uint>("SET_VEHICLE_INDICATOR_LIGHTS", vehicle, 1, enabled);
            }
            catch (Exception ex)
            {
                Main.Log("SET_VEHICLE_INDICATOR_LIGHTS failed: " + ex.Message);
            }
        }

        internal static void StartScenario(Ped ped, string scenario)
        {
            if (ped == null || !ped.Exists() || string.IsNullOrWhiteSpace(scenario)) return;

            try
            {
                NativeFunction.CallByName<uint>("TASK_START_SCENARIO_IN_PLACE", ped, scenario, -1, true);
            }
            catch (Exception ex)
            {
                Main.Log("TASK_START_SCENARIO_IN_PLACE failed: " + ex.Message);
            }
        }

        internal static void Say(string speaker, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Game.DisplaySubtitle("~b~" + speaker + ":~w~ " + text, 5000);
        }

        internal static string Cardinal(float heading)
        {
            heading = (heading % 360f + 360f) % 360f;
            if (heading < 22.5f || heading >= 337.5f) return "north";
            if (heading < 67.5f) return "north-east";
            if (heading < 112.5f) return "east";
            if (heading < 157.5f) return "south-east";
            if (heading < 202.5f) return "south";
            if (heading < 247.5f) return "south-west";
            if (heading < 292.5f) return "west";
            return "north-west";
        }
    }
}
