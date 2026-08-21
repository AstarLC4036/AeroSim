using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using AeroSim.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AeroSim.AircraftModules.RWRModule;

namespace AeroSim.UI
{
    public class ThreatDrawer : MonoBehaviour
    {
        private static ThreatDrawer instance;
        public static ThreatDrawer Instance => instance;

        public bool drawerEnabled = false;
        public List<ReceivedObjectData> receivedObjects;
        public List<Missile> detectedMissiles;

        private float mslCursorSize;
        private Aircraft parentAircraft;
        private RWRModule rwr;
        private MAWModule maw;
        private RectTransform rwrRect;
        private GUIStyle targetLabelStyle;

        private static Action<Aircraft> onInit = (aircraft) => { };
        public static void GlobalInit(Aircraft aircraft)
        {
            onInit(aircraft);
        }

        private void Awake()
        {
            instance = this;
        }

        private void OnValidate()
        {
            if (HUDDrawer.Instance != null)
                mslCursorSize = HUDDrawer.Instance.mslCursorSize;
        }

        // Use this for initialization
        void Start()
        {
            onInit += Init;
        }

        void Init(Aircraft aircraft)
        {
            parentAircraft = aircraft;
            rwr = parentAircraft.rwr;
            maw = parentAircraft.maw;
            if (rwr != null)
            {
                receivedObjects = rwr.receivedObjects;
            }
            if (maw != null)
            {
                detectedMissiles = maw.mslDetected;
            }
            if(rwr != null || maw != null)
            {
                drawerEnabled = true;
            }

            rwrRect = AircraftUI.RWR;
            mslCursorSize = HUDDrawer.Instance.mslCursorSize;

            targetLabelStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                font = AircraftUI.RwrFont,
                richText = true
            };
        }

        void OnGUI()
        {
            if (drawerEnabled)
            {
                if (maw != null)
                    UpdateMAWObjects();
                if (rwr != null)
                    UpdateRWRObjects();
            }
        }

        void DrawMSL(Vector3 position)
        {
            Vector3 mslPosition = position;
            Vector3 posOnScreen = Camera.main.WorldToScreenPoint(mslPosition);
            float dst = Vector3.Distance(mslPosition, Aircraft.main.transform.position);
            float dstRounded = Mathf.Round(dst / 100) / 10; // .../1000*10(kilometre) = .../100
            if (posOnScreen.z > 0)
            {
                GUI.DrawTexture(new Rect(
                    posOnScreen.x - mslCursorSize / 2,
                    Screen.height - posOnScreen.y - mslCursorSize / 2,
                    mslCursorSize, mslCursorSize),
                    HUDDrawer.Instance.mslPointer);
                GUI.Label(new Rect(
                    posOnScreen.x - mslCursorSize / 2,
                    Screen.height - posOnScreen.y - mslCursorSize / 2 + 50,
                    mslCursorSize, mslCursorSize), $"<color=red>MSL</color>\n<color=red>{dstRounded} km</color>", targetLabelStyle);
            }
        }

        void UpdateMAWObjects()
        {
            foreach (Missile msl in detectedMissiles)
            {
                if(msl.parentAircraft != parentAircraft)
                    DrawMSL(msl.transform.position);
            }
        }

        void UpdateRWRObjects()
        {
            // update rwr display
            Vector3 fwd = parentAircraft.transform.forward;
            float rwrRotateAngle = Mathf.Atan2(fwd.x, fwd.z);

            for (int i = 0; i < receivedObjects.Count; i++)
            {
                ReceivedObjectData objectData = receivedObjects[i];

                //Vector3 localPos = parentAircraft.transform.InverseTransformPoint(objectData.position);
                //float magnitude = localPos.magnitude;
                //float magnitudeXZ = new Vector3(localPos.x, 0, localPos.z).magnitude;
                //float mCoeff = magnitude / magnitudeXZ;

                Vector3 deltaPos = objectData.position - parentAircraft.transform.position;
                float angle = Mathf.Atan2(deltaPos.x, deltaPos.y);
                float deltaAngle = angle - rwrRotateAngle;
                float magnitude = Mathf.Clamp(deltaPos.magnitude, 0, rwr.range * 1000);
                Vector3 transformedPos = new Vector3(magnitude * Mathf.Sin(deltaAngle), 0, magnitude * Mathf.Cos(deltaAngle));

                //if (objectData.isLockingUs && objectData.targetType == TargetType.MSL && (maw == null ? true : detectedMissiles.Exists(x => x.gameObject == objectData.signalTransmitter)))
                //{
                //    DrawMSL(objectData.signalTransmitter.transform.position);
                //}

                //On RWR display
                if (magnitude < rwr.range * 1000 && !objectData.isLockingUs)
                {
                    GUI.Label(
                        new Rect(
                            rwrRect.position.x + transformedPos.x / (rwr.range * 1000) / 2 * rwrRect.rect.width - 25,
                            Screen.height - (rwrRect.position.y + transformedPos.z / (rwr.range * 1000) / 2 * rwrRect.rect.height + 25),
                            50, 50),
                        $"<color=green>{(objectData.targetType == TargetType.Unknown ? "U" : objectData.displayText)}</color>", targetLabelStyle);

                    if (objectData.needAlarm)
                    {
                        GUI.DrawTexture(new Rect(
                                rwrRect.position.x + transformedPos.x / (rwr.range * 1000) / 2 * rwrRect.rect.width - 25,
                                Screen.height - (rwrRect.position.y + transformedPos.z / (rwr.range * 1000) / 2 * rwrRect.rect.height) - 25,
                                50, 50),
                                AircraftUI.RwrAlarmRing);
                    }

                    //GUI.Label(
                    //    new Rect(
                    //        rwr.position.x + localPos.x * mCoeff / (range * 1000) * rwr.rect.width - 25,
                    //        Screen.height - (rwr.position.y + localPos.z * mCoeff / (range * 1000) * rwr.rect.height + 25),
                    //        50, 50),
                    //    $"<color=green>{(objectData.targetType == TargetType.Unknown ? "U" : objectData.displayText)}</color>", targetLabelStyle);

                    //if (objectData.needAlarm)
                    //{
                    //    GUI.DrawTexture(new Rect(
                    //            rwr.position.x + localPos.x * mCoeff / (range * 1000) * rwr.rect.width - 25,
                    //            Screen.height - (rwr.position.y + localPos.z * mCoeff / (range * 1000) * rwr.rect.height) - 25,
                    //            50, 50),
                    //            AircraftUI.RwrAlarmRing);
                    //}
                }

                //If we get locked, draw a line from the target to the center of the RWR display
                else if (objectData.isLockingUs)
                {
                    GUI.Label(
                        new Rect(
                            rwrRect.position.x + transformedPos.x / (rwr.range * 1000) / 2 * rwrRect.rect.width - 25,
                            Screen.height - (rwrRect.position.y + transformedPos.z / (rwr.range * 1000) / 2 * rwrRect.rect.height + 25),
                            50, 50),
                        $"<color=green>{(objectData.targetType == TargetType.Unknown ? "U" : objectData.displayText)}</color>", targetLabelStyle);

                    GUI.DrawTexture(
                        new Rect(
                            rwrRect.position.x + transformedPos.x / (rwr.range * 1000) / 2 * rwrRect.rect.width - 25,
                            Screen.height - (rwrRect.position.y + transformedPos.z / (rwr.range * 1000) / 2 * rwrRect.rect.height) - 25,
                            50, 50),
                        AircraftUI.RwrAlarmRing);

                    GL.PushMatrix();
                    GL.LoadOrtho();
                    AircraftUI.LineMaterial.SetPass(0);

                    GL.Begin(GL.LINE_STRIP);
                    GL.Color(Color.green);

                    GL.Vertex3((rwrRect.position.x + transformedPos.x / (rwr.range * 1000) / 2 * rwrRect.rect.width) / Screen.width,
                               ((rwrRect.position.y + transformedPos.z / (rwr.range * 1000) / 2 * rwrRect.rect.height)) / Screen.height,
                                0);
                    GL.Vertex3(rwrRect.position.x / Screen.width,
                               (rwrRect.position.y) / Screen.height,
                               0);
                    GL.End();
                    GL.PopMatrix();
                }
            }
        }
    }
}