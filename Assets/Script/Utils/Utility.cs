using AeroSim.AircraftModules;
using Unity.VisualScripting;
using UnityEngine;

namespace AeroSim.Utility
{
    public class Utilities
    {
        public static bool ObjectVisible(Object obj, Camera cam)
        {
            if(obj.GetType() == typeof(Transform) || obj.GetType() == typeof(GameObject)) 
            {
                Bounds bounds = obj.GetComponent<Renderer>().bounds;

                return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), bounds);
            }
            else
            {
                return false;
            }
        }

        public static Rect CalcucateTextureScreenPos(Vector3 pos, float size)
        {
            return new Rect(pos.x - size / 2, Screen.height - pos.y - size / 2, size, size);
        }

        public static string RadarStatusString(RadarModule radar)
        {
            string mode = radar.radarMode.ToString();
            string processing = radar.radarProcessing.ToString();

            if (processing == "None")
                processing = "";

            return $"{mode} {processing}";
        }
    }
}
