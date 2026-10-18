using AeroSim.Utils;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

namespace AeroSim.Terrain
{
    // Mesh Generation based on Jobs/Burst system was written by LLM, simply checked.
    // 我保留了一些手写，这样你才知道你读的是史山。

    public class TerrainGenerator : MonoBehaviour
    {
        public enum TerrainNoiseType
        {
            Perlin,
        }

        [Serializable]
        public struct NoiseLayer
        {
            public TerrainNoiseType type;
            public int seed;
            public float freqency;
            public float strength;
            [NonSerialized, HideInInspector]
            public PerlinNoise noise;

            public void BuildNoise()
            {
                noise = new PerlinNoise(seed);
            }

            public float GetHeight(float posX, float posY)
            {
                float height = noise.Sample(posX * freqency, posY * freqency) * strength;
                return height;
            }
        }

        [Header("Terrain Settings")]
        public NoiseLayer[] noiseLayers;
        public Dictionary<int, float> splitLodLevels = new Dictionary<int, float>()
        {
            { 0, 7490 },
            { 1, 3740 },
            { 2, 1870 },
            { 3, 935 },
            { 4, 467 },
            { 5, 234 }
        };
        public Dictionary<int, float> mergeLodLevels = new Dictionary<int, float>()
        {
            { 0, 11200 },
            { 1, 5610 },
            { 2, 2800 },
            { 3, 1400 },
            { 4, 700 },
            { 5, float.PositiveInfinity } // Not merge
        };

        [Header("Terrain Generation")]
        public List<TerrainNode> nodes = new List<TerrainNode>();
        public int rootSize = 4096;
        public int rootRes = 128;

        [Tooltip("Baked DEM dataset driving the chunk heights. Empty keeps the noise layers in charge.")]
        public DemAsset demAsset;

        [Header("Performance")]
        public int maxRootUpdateCount = 64;
        public int generationRadius = 32;

        // Root
        private readonly Dictionary<Vector2Int, TerrainNode> roots = new Dictionary<Vector2Int, TerrainNode>();
        private Queue<Vector2Int> pendingRootsLoad = new Queue<Vector2Int>();
        private Queue<Vector2Int> pendingRootsUnload = new Queue<Vector2Int>();

        // LOD
        private Queue<TerrainNode> splitedNodes = new Queue<TerrainNode>();
        private Queue<TerrainNode> mergedNodes = new Queue<TerrainNode>();

        // Scratch containers for retiring subtrees, kept around so the release path allocates nothing.
        private readonly List<TerrainNode> retiredNodes = new List<TerrainNode>();
        private readonly HashSet<TerrainNode> retiredSet = new HashSet<TerrainNode>();
        private Predicate<TerrainNode> retiredPredicate;

        // Copy of noiseLayers the jobs can read: NoiseLayer is a plain struct, so the inspector
        // array is copied straight into native memory.
        private NativeArray<NoiseLayer> layerArray;

        // Native copy of the baked DEM, or null while the noise layers are in charge.
        private DemTerrainData dem;

        // 1x1 stand-in handed to chunks that have no DEM: job containers must always be constructed.
        private NativeArray<ushort> demPlaceholder;

        // One index buffer per res. The topology of a chunk only depends on its res, so every chunk
        // with the same res shares one buffer instead of rebuilding 6 * res * res indices each time.
        private readonly Dictionary<int, NativeArray<int>> indexBuffers = new Dictionary<int, NativeArray<int>>();
        private Camera mainCamera;

        // Named markers so the terrain cost is visible by name in the Profiler. Without them it only
        // shows up inside the collapsed FixedUpdate.ScriptRunBehaviourFixedUpdate summary row.
        private static readonly ProfilerMarker LodMarker = new ProfilerMarker("Terrain.UpdateLod");
        private static readonly ProfilerMarker MeshMarker = new ProfilerMarker("Terrain.UpdateMesh");

        // --- Lifecycle ---

        public void Start()
        {
            mainCamera = Camera.main;
            BuildLayerArray();
            BuildDemData();
        }

        // Temporary method
        public void FixedUpdate()
        {
            if (mainCamera != null)
            {
                UpdateLod();   // no camera means nothing to measure distances against
                UpdateRing(mainCamera.transform.position);
            }
            UpdateMesh();
        }

        //private void Update()
        //{
        //    if (!Application.isPlaying || nodes == null) return;

        //    foreach (var node in nodes)
        //    {
        //        if (node != null) node.TryComplete();
        //    }
        //}

        private void UpdateRing(Vector3 cameraPos)
        {
            // get camera cell
            Vector2 cam = new Vector2(cameraPos.x - FloatingOrigin.origin.x, cameraPos.z - FloatingOrigin.origin.z);
            Vector2Int camCell = new Vector2Int(
                Mathf.FloorToInt(cam.x / rootSize), Mathf.FloorToInt(cam.y / rootSize));

            // register nodes to be loaded
            for (int x = -generationRadius; x <= generationRadius; x++)
                for (int z = -generationRadius; z <= generationRadius; z++)
                {
                    var cell = new Vector2Int(camCell.x + x, camCell.y + z);
                    if (!roots.ContainsKey(cell) && !pendingRootsLoad.Contains(cell)) pendingRootsLoad.Enqueue(cell);
                }

            // register nodes to be unloaded
            foreach (Vector2Int cell in roots.Keys)
            {
                bool outsideX = cell.x > camCell.x + generationRadius || cell.x < camCell.x - generationRadius;
                bool outsideY = cell.y > camCell.y + generationRadius || cell.y < camCell.y - generationRadius;
                if ((outsideX || outsideY) && !pendingRootsUnload.Contains(cell))
                {
                    pendingRootsUnload.Enqueue(cell);
                }
            }

            // create
            for (int i = 0; i < maxRootUpdateCount && pendingRootsLoad.Count > 0; i++)
            {
                var cell = pendingRootsLoad.Dequeue();
                if (roots.ContainsKey(cell)) continue;

                var root = new TerrainNode
                {
                    offset = new Vector2(cell.x * rootSize, cell.y * rootSize),
                    size = rootSize,
                    res = rootRes,
                    lodLevel = 0,
                };
                root.RentView();
                roots[cell] = root;
                nodes.Add(root);
            }

            // release
            for (int i = 0; i < maxRootUpdateCount && pendingRootsUnload.Count > 0; i++)
            {
                var cell = pendingRootsUnload.Dequeue();
                if (!roots.ContainsKey(cell)) continue;

                var root = roots[cell];
                roots.Remove(cell);
                root.ReleaseView();
                ReleaseSubtree(root);
            }
        }

        // --- LOD ---
        public void UpdateLod()
        {
            LodMarker.Begin();

            foreach (var node in nodes)
            {
                int state = node.UpdateLod(mainCamera.transform.position, splitLodLevels, mergeLodLevels);

                if (state == 1)
                {
                    node.BuildSubNodes();
                    foreach (var subNode in node.subNodes)
                    {
                        subNode.UpdateLod(mainCamera.transform.position, splitLodLevels, mergeLodLevels); // check once before build to avoid unnecessary performace consumption
                        splitedNodes.Enqueue(subNode);
                    }
                }
                else if (state == 2)
                {
                    // The children keep rendering even though they leave the node list right below:
                    // their views are only released once the parent has its own mesh again.
                    foreach (var subNode in node.subNodes)
                    {
                        mergedNodes.Enqueue(subNode);
                    }
                }
            }

            // 1000 -> how many node will be handled in once LOD update
            for (int i = 0; i < 1000; i++)
            {
                if (splitedNodes.Count == 0)
                    break;
                nodes.Add(splitedNodes.Dequeue());
            }
            for (int i = 0; i < 1000; i++)
            {
                if (mergedNodes.Count == 0)
                    break;
                TerrainNode node = mergedNodes.Dequeue();
                nodes.RemoveAll(x => x == node);
            }

            LodMarker.End();
        }

        // Playing mode only
        public void UpdateMesh()
        {
            if (!layerArray.IsCreated) return;   // Start has not run yet

            MeshMarker.Begin();

            foreach (var node in nodes)
            {
                if (node == null) continue;

                // Pump whatever this node has in flight, whatever its state: it can hold a job from a
                // previous state (it was building, then split), and a job nobody completes keeps its
                // native buffers alive for good.
                node.TryComplete();

                if (node.treeState == TerrainNode.NodeState.Splitting) // build the four new sub-nodes
                {
                    if (node.subNodes == null) continue;

                    bool allVisible = true;
                    for (int i = 0; i < node.subNodes.Length; i++)
                    {
                        TerrainNode subNode = node.subNodes[i];

                        if (subNode.filter == null) subNode.RentView();

                        if (!subNode.HasSubNode && subNode.NeedsBuild)
                        {
                            subNode.ScheduleBuild(layerArray, GetIndexBuffer(subNode.Res), DemLevelFor(subNode));
                        }

                        subNode.TryComplete();           // pump whatever this child has in flight
                        allVisible &= subNode.IsVisible; // recursive: a child that split again counts too
                    }

                    if (allVisible)
                    {
                        // Everything the children cover is drawn, so the coarse view can go away.
                        node.ReleaseView();
                        node.treeState = TerrainNode.NodeState.Branch;
                    }
                }
                else if (node.treeState == TerrainNode.NodeState.Merging) // get my own mesh back
                {
                    node.RentView();

                    if (node.NeedsBuild) node.ScheduleBuild(layerArray, GetIndexBuffer(node.Res), DemLevelFor(node));
                    node.TryComplete();

                    bool ready = node.filter != null && node.filter.sharedMesh != null && !node.IsBuilding;
                    if (ready && node.subNodes != null)
                    {
                        // Only now may the children go away, otherwise there would be a hole.
                        for (int i = 0; i < node.subNodes.Length; i++) ReleaseSubtree(node.subNodes[i]);
                        node.DisposeSubNodes();
                        node.treeState = TerrainNode.NodeState.Leaf;
                    }
                }
                else
                {
                    // Leaf: keep any build in flight moving. This also covers starting Play without
                    // pressing Generate first. A Branch node renders nothing of its own, so it is skipped.
                    if (node.treeState == TerrainNode.NodeState.Leaf && node.NeedsBuild)
                    {
                        node.ScheduleBuild(layerArray, GetIndexBuffer(node.Res), DemLevelFor(node));
                    }
                    node.TryComplete();
                }
            }

            MeshMarker.End();
        }

        /// <summary>
        /// Repairs nodes whose serialised state drifted out of sync with their children (for example
        /// after a scene was saved from an older buggy session). The hand placed views are untouched,
        /// only the tree bookkeeping is reset - press Generate or Play afterwards to rebuild.
        /// </summary>
        [ContextMenu("Reset Node States")]
        private void ResetNodeStates()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TerrainNode node = nodes[i];
                if (node == null) continue;

                node.subNodes = null;
                node.parentNode = null;
                node.treeState = TerrainNode.NodeState.Leaf;
            }

            Debug.Log($"TerrainGenerator: reset {nodes.Count} node(s) to Leaf. " +
                      "Press Generate, or just Play, to rebuild them.");
        }

        /// <summary>
        /// Debug helper: right click the component in the Inspector and pick "Dump Terrain Tree"
        /// (works while playing). activeSelf true + activeInHierarchy false means the parent object
        /// of the chunks is inactive, which is the usual reason a chunk exists but never renders.
        /// </summary>
        [ContextMenu("Dump Terrain Tree")]
        private void DumpTree()
        {
            string cam = mainCamera != null ? mainCamera.name : "NULL";
            Debug.Log($"TerrainGenerator: nodes={nodes.Count} layerArray={layerArray.IsCreated} " +
                      $"pool={(TerrainMeshPool.Instance != null ? "ok" : "MISSING")} camera={cam} " +
                      $"isPlaying={Application.isPlaying}");

            for (int i = 0; i < nodes.Count; i++)
            {
                TerrainNode n = nodes[i];
                if (n == null) { Debug.Log($"[{i}] null"); continue; }

                string view = "no filter";
                if (n.filter != null)
                {
                    GameObject go = n.filter.gameObject;
                    string mesh = n.filter.sharedMesh != null
                        ? $"verts={n.filter.sharedMesh.vertexCount}"
                        : "mesh=NULL";
                    view = $"'{go.name}' activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} " +
                           $"{mesh} pos={go.transform.position} layer={go.layer}";
                }

                Debug.Log($"[{i}] lod={n.lodLevel} size={n.size} res={n.Res} offset={n.offset} " +
                          $"state={n.treeState} visible={n.IsVisible} building={n.IsBuilding} " +
                          $"subs={(n.subNodes == null ? 0 : n.subNodes.Length)} | {view}");
            }
        }

        // --- Mesh Generating --
        /// <summary>Schedules every node's chunk (outside play mode it also finishes them right away).</summary>
        public void CreateMesh()
        {
            BuildLayerArray();
            BuildDemData();     // pressing Generate in the inspector uses the DEM too

            if (nodes == null) return;

            bool scheduled = false;
            foreach (var node in nodes)
            {
                if (node == null) continue;
                scheduled |= node.ScheduleBuild(layerArray, GetIndexBuffer(node.Res), DemLevelFor(node));
            }

            // Nothing pumps the jobs outside play mode, so finish them here: pressing Generate in
            // the inspector still shows the result immediately.
            if (scheduled && !Application.isPlaying) CompleteAll();
        }

        /// <summary>Waits for every pending chunk and uploads its mesh.</summary>
        public void CompleteAll()
        {
            if (nodes == null) return;

            foreach (var node in nodes)
            {
                if (node != null) node.CompleteBuild();
            }
        }

        private void OnDisable()
        {
            // A job must not outlive the buffers it writes into, and native memory is not garbage
            // collected: everything native this generator handed out is released here.
            ReleaseAllNodes();     // finishes every pending job, so the DEM arrays are free to go

            DisposeIndexBuffers();
            DisposeLayerArray();
            DisposeDemData();
        }

        /// <summary>
        /// Loads the dataset once. Start, CreateMesh and the inspector's Generate button can all ask
        /// for it, and a failure simply leaves the noise layers active.
        /// </summary>
        private void BuildDemData()
        {
            // The job validator rejects unconstructed containers, so even a chunk without a DEM has to
            // be handed a valid NativeArray. 'DemLevel.active' then selects the noise path.
            if (!demPlaceholder.IsCreated)
            {
                demPlaceholder = new NativeArray<ushort>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (dem != null || demAsset == null) return;

            dem = DemTerrainData.Load(demAsset);
        }

        private void DisposeDemData()
        {
            if (dem != null)
            {
                dem.Dispose();
                dem = null;
            }

            if (demPlaceholder.IsCreated) demPlaceholder.Dispose();
            demPlaceholder = default;
        }

        /// <summary>
        /// DEM level for a chunk. Chunks outside the dataset - and every chunk when no asset is
        /// assigned - get the inactive placeholder level, which sends the job down the noise path.
        /// </summary>
        private DemLevel DemLevelFor(TerrainNode node)
        {
            DemLevel level = dem == null ? default : dem.GetLevel(node);
            if (level.active) return level;

            level.heights = demPlaceholder;
            level.width = 1;
            level.height = 1;
            level.active = false;
            return level;
        }

        /// <summary>
        /// Releases a whole subtree and removes every node of it from 'nodes'.
        /// Call it for each of the four children once a merge has uploaded the parent's mesh.
        /// </summary>
        private void ReleaseSubtree(TerrainNode node)
        {
            if (node == null) return;

            retiredNodes.Clear();
            retiredSet.Clear();

            node.ReleaseSubtree(retiredNodes);

            for (int i = 0; i < retiredNodes.Count; i++) retiredSet.Add(retiredNodes[i]);

            // RemoveAll rather than Remove: 'nodes' may still hold duplicates.
            retiredPredicate ??= n => n == null || retiredSet.Contains(n);
            nodes.RemoveAll(retiredPredicate);
        }

        /// <summary>Finishes pending jobs and releases every node, then empties the runtime bookkeeping.</summary>
        private void ReleaseAllNodes()
        {
            if (nodes == null) return;

            CompleteAll();          // finish every pending job BEFORE freeing any buffer

            retiredNodes.Clear();

            for (int i = 0; i < nodes.Count; i++)
            {
                TerrainNode node = nodes[i];
                if (node != null && node.parentNode == null) node.ReleaseSubtree(retiredNodes);  // roots, recursive
            }

            splitedNodes.Clear();
            mergedNodes.Clear();
            retiredNodes.Clear();

            // Runtime LOD appends to this list; in the editor it holds the hand authored chunks which
            // belong to the scene, so it is only cleared while playing.
            if (Application.isPlaying) nodes.Clear();
        }

        private void BuildLayerArray()
        {
            int count = noiseLayers == null ? 0 : noiseLayers.Length;

            if (!layerArray.IsCreated || layerArray.Length != count)
            {
                DisposeLayerArray();
                layerArray = new NativeArray<NoiseLayer>(count, Allocator.Persistent);
            }

            for (int i = 0; i < count; i++)
            {
                // PerlinNoise is only a seed and is not serialised, so reseed it here: an inspector
                // seed change then takes effect on the next Generate, without leaving play mode.
                // It has to be written back into the array - a foreach loop would only touch a copy.
                noiseLayers[i].BuildNoise();
                layerArray[i] = noiseLayers[i];
            }
        }

        private void DisposeLayerArray()
        {
            if (layerArray.IsCreated) layerArray.Dispose();
        }

        public NativeArray<int> GetIndexBuffer(int res)
        {
            if (indexBuffers.TryGetValue(res, out NativeArray<int> cached) && cached.IsCreated) return cached;

            int side = res + 1;
            var indices = new NativeArray<int>(6 * res * res, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            int t = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = z * side + x;
                    indices[t++] = i;              // (x,   z)
                    indices[t++] = i + side;       // (x,   z+1)
                    indices[t++] = i + 1;          // (x+1, z)

                    indices[t++] = i + 1;          // (x+1, z)
                    indices[t++] = i + side;       // (x,   z+1)
                    indices[t++] = i + side + 1;   // (x+1, z+1)
                }
            }

            indexBuffers[res] = indices;
            return indices;
        }

        private void DisposeIndexBuffers()
        {
            foreach (var buffer in indexBuffers.Values)
            {
                if (buffer.IsCreated) buffer.Dispose();
            }
            indexBuffers.Clear();
        }
    }
}
