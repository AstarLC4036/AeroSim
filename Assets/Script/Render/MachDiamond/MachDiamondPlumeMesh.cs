using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using AeroSim.AircraftModules;
#endif

namespace AeroSim.Render
{
    /// <summary>
    /// Container mesh for <see cref="MachDiamondPlumeController"/>.
    ///
    /// Canonical shape: closed cylinder, radius 1, local z in [0,1], axis = +Z.
    /// The controller scales it so that the transform scale *is* the metric size
    /// of the shading volume (radius on x/y, length on z) — the shader reads the
    /// same numbers from _ObjScale, so mesh and volume always agree.
    ///
    /// The mesh is only a container: nothing is shaded from its surface. Both
    /// end caps exist so that the volume still produces a fragment when the
    /// camera looks straight down the plume axis (Cull Front in the shader then
    /// yields exactly one fragment per ray).
    /// </summary>
    public static class MachDiamondPlumeMesh
    {
        public const int DefaultRadialSegments = 24;
        public const string DefaultMeshName = "MachDiamondPlumeCylinder";

        public static Mesh Build(int radialSegments = DefaultRadialSegments)
        {
            radialSegments = Mathf.Max(3, radialSegments);
            int rings = radialSegments + 1;

            var vertices = new Vector3[rings * 2 + 2];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];

            for (int i = 0; i < rings; i++)
            {
                float u = (float)i / radialSegments;
                float ang = u * Mathf.PI * 2f;
                float cs = Mathf.Cos(ang);
                float sn = Mathf.Sin(ang);

                vertices[i] = new Vector3(cs, sn, 0f);
                normals[i] = new Vector3(cs, sn, 0f);
                uvs[i] = new Vector2(u, 0f);

                vertices[rings + i] = new Vector3(cs, sn, 1f);
                normals[rings + i] = new Vector3(cs, sn, 0f);
                uvs[rings + i] = new Vector2(u, 1f);
            }

            int bottomCenter = rings * 2;
            int topCenter = rings * 2 + 1;
            vertices[bottomCenter] = new Vector3(0f, 0f, 0f);
            normals[bottomCenter] = Vector3.back;
            uvs[bottomCenter] = new Vector2(0.5f, 0.5f);
            vertices[topCenter] = new Vector3(0f, 0f, 1f);
            normals[topCenter] = Vector3.forward;
            uvs[topCenter] = new Vector2(0.5f, 0.5f);

            var triangles = new int[radialSegments * 12];
            int t = 0;
            for (int i = 0; i < radialSegments; i++)
            {
                int b0 = i, b1 = i + 1;
                int t0 = rings + i, t1 = rings + i + 1;

                // side, outward facing: b0 -> b1 -> t0 matches the outward normals
                // and the end caps (the old order faced inwards, which made a
                // Cull Front shader render the near wall and clip the volume away)
                triangles[t++] = b0; triangles[t++] = b1; triangles[t++] = t0;
                triangles[t++] = b1; triangles[t++] = t1; triangles[t++] = t0;
                // nozzle cap (z = 0, normal -Z)
                triangles[t++] = bottomCenter; triangles[t++] = b1; triangles[t++] = b0;
                // tip cap (z = 1, normal +Z)
                triangles[t++] = topCenter; triangles[t++] = t0; triangles[t++] = t1;
            }

            var mesh = new Mesh { name = DefaultMeshName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Reuses an existing container mesh, otherwise builds a runtime one.</summary>
        public static Mesh GetOrCreate(MeshFilter filter)
        {
            if (filter == null) return null;

            Mesh current = filter.sharedMesh;
            if (current != null && current.name == DefaultMeshName) return current;

            Mesh mesh = Build();
            mesh.hideFlags = HideFlags.DontSave;
            filter.sharedMesh = mesh;
            return mesh;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only asset authoring shared by the setup menu and by
    /// <see cref="MachDiamondPlumeController"/> (so dropping the component on an
    /// object by hand also binds a real material asset instead of a runtime one).
    /// </summary>
    internal static class MachDiamondPlumeAssets
    {
        public const string ShaderName = "AeroSim/MachDiamond/Plume";
        public const string DefaultFolder = "Assets/Script/Render/MachDiamond";

        /// <summary>Folder this script actually lives in, so moving it keeps working.</summary>
        public static string Folder
        {
            get
            {
                foreach (string guid in AssetDatabase.FindAssets("MachDiamondPlumeController t:MonoScript"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != "MachDiamondPlumeController") continue;
                    string dir = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) return dir.Replace('\\', '/');
                }
                return DefaultFolder;
            }
        }

        public static string MeshPath => Folder + "/MachDiamondPlumeCylinder.asset";
        public static string MaterialPath => Folder + "/MachDiamondPlume.mat";

        public static Mesh EnsureMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh != null) return mesh;

            mesh = MachDiamondPlumeMesh.Build();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) ?? mesh;
        }

        public static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[MachDiamond] Shader '{ShaderName}' not found. Is MachDiamondPlume.shader imported?");
                return null;
            }

            material = new Material(shader) { name = "MachDiamondPlume" };
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, MaterialPath);
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) ?? material;
        }
    }

    /// <summary>One-click authoring setup: GameObject &gt; AeroSim &gt; Mach Diamond Plume.</summary>
    public static class MachDiamondPlumeSetup
    {
        [MenuItem("GameObject/AeroSim/Mach Diamond Plume", false, 10)]
        public static void CreatePlume(MenuCommand command)
        {
            Material material = MachDiamondPlumeAssets.EnsureMaterial();
            Mesh mesh = MachDiamondPlumeAssets.EnsureMesh();
            if (material == null || mesh == null) return;

            var parent = command.context as GameObject;
            var go = new GameObject("MachDiamondPlume");
            Undo.RegisterCreatedObjectUndo(go, "Create Mach Diamond Plume");
            if (parent != null) GameObjectUtility.SetParentAndAlign(go, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            var controller = go.AddComponent<MachDiamondPlumeController>();
            controller.engine = parent != null ? parent.GetComponentInParent<EngineModule>() : null;
            controller.nozzleRadius = 0.45f;
            controller.plumeLength = 6.0f;

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
#endif
}
