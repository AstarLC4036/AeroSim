using AeroSim.AeroPhysics;
using AeroSim.AircraftModules;
using AeroSim.Cockpit;
using AeroSim.InputSystem;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.AircraftModules
{
    public class HUDHolder : AircraftModule
    {
        public Transform hudTransform;
        public Renderer hudRenderer;
        public Vector3 position;
        public DynamicHUDDrawer hudDrawer;
        public TMP_FontAsset font;
        public Color bgColor;
        public int targetMatIndex = 0;

        public float hudPhysicalSize = 1;

        void Start()
        {
            RenderPipelineManager.beginCameraRendering += MoveHUD;
        }

        public override void Init(Aircraft aircraft)
        {
            base.Init(aircraft);
            if (parentAircraft.isControlling)
                InitCanvas();
        }

        void InitCanvas()
        {
            hudDrawer?.Dispose();          // On a repeated Init (switching aircraft / reload) the old drawer's Buffer/RT would leak
            if (hudDrawer == null)
            {
                hudDrawer = new DynamicHUDDrawer(new Vector2Int(1024, 1024), bgColor);
            }
            else
            {
                hudDrawer.size = new Vector2Int(1024, 1024);
                hudDrawer.bgColor = bgColor;
            }
            hudDrawer.InitCanvas();
            hudDrawer.SetFont(font);
            hudDrawer.SetAircraft(parentAircraft);
            hudDrawer.parentHolder = this;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            hudRenderer.GetPropertyBlock(block, targetMatIndex);
            //block.SetVector("_BaseMap_ST", new Vector4(mfd.tiling.x, mfd.tiling.y, mfd.offset.x, mfd.offset.y));
            block.SetTexture("_BaseMap", hudDrawer.canvasTexture);
            hudRenderer.SetPropertyBlock(block, targetMatIndex);
        }

        private void OnApplicationQuit()
        {
            RenderPipelineManager.beginCameraRendering -= MoveHUD;
            hudDrawer?.Dispose();          // Was never released before → ComputeBuffer/RenderTexture leak
            hudDrawer = null;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= MoveHUD;
            hudDrawer?.Dispose();
            hudDrawer = null;
        }

        public void Update()
        {
            if (parentAircraft != null && parentAircraft.isControlling && hudDrawer != null)
                hudDrawer.UpdateCanvas();
        }

        public void MoveHUD(ScriptableRenderContext context, Camera cam)
        {
            if (parentAircraft.isControlling)
            {
                if (CameraController.CurrentView.view == CameraController.CameraView.ViewType.Cockpit)
                {
                    if (!hudTransform.gameObject.activeSelf)
                    {
                        hudTransform.gameObject.SetActive(true);
                    }

                    hudTransform.position = Camera.main.transform.position 
                        + position.x * parentAircraft.transform.right
                        + position.y * parentAircraft.transform.up
                        + position.z * parentAircraft.transform.forward;
                }
                else
                {
                    if (hudTransform.gameObject.activeSelf)
                    {
                        hudTransform.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}