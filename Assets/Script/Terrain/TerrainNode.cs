using AeroSim.Utils;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using static AeroSim.Terrain.TerrainGenerator;

namespace AeroSim.Terrain
{
    /// <summary>
    /// One terrain chunk. Its mesh is built by Burst jobs: <see cref="ScheduleBuild"/> dispatches
    /// them and <see cref="TryComplete"/> uploads the result on a later frame, so the main thread
    /// never sits in the noise loop.
    /// </summary>
    [Serializable]
    public class TerrainNode
    {
        public enum NodeState
        {
            Leaf,
            Splitting,
            Branch,
            Merging
        }


        public Vector2 offset; // noise space origin of this chunk, i.e. the world position of vertex 0
        public int size = 1;   // chunk edge length in units
        public int res;        // quads per edge, so res + 1 vertices per edge
        public MeshFilter filter;
        public int lodLevel = 0;

        // The tree is runtime only. Serialising it writes parent <-> subNodes cycles into the scene
        // (Unity aborts that walk with "serialization depth limit exceeded") and lets a stale state
        // survive a reload - which is exactly how a node ends up Branch without children and never
        // renders again. Everything below is rebuilt from scratch every time the game starts.
        [NonSerialized] private bool ownsFilter;   // true when 'filter' came from TerrainMeshPool
        [NonSerialized] public TerrainNode parentNode;
        [NonSerialized] public TerrainNode[] subNodes;
        [NonSerialized] public NodeState treeState = NodeState.Leaf;

        /// <summary>Quads per edge, never below 1: res = 0 would make spacing infinite and every vertex NaN.</summary>
        public int Res => Mathf.Max(1, res);

        /// <summary>Edge length in units, never below 1: a chunk without size covers nothing.</summary>
        public int Size => Mathf.Max(1, size);

        /// <summary>Distance between two neighbouring vertices.</summary>
        public float Spacing => Size * 1f / Res;

        public int LodLevel => lodLevel;

        public NodeState TreeState => treeState;

        public bool HasSubNode => subNodes != null && subNodes.Length != 0;

        /// <summary>True while a build has been scheduled but not uploaded yet.</summary>
        public bool IsBuilding => hasPendingJob;
        public bool NeedsBuild => filter != null && !hasPendingJob && filter.sharedMesh == null;

        /// <summary>
        /// True when this node, or its whole subtree, is currently showing a mesh. A parent may only
        /// release its own view once all four children are visible, otherwise a hole appears.
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (HasSubNode)
                {
                    for (int i = 0; i < subNodes.Length; i++)
                    {
                        if (subNodes[i] == null || !subNodes[i].IsVisible) return false;
                    }
                    return true;
                }

                return filter != null && filter.sharedMesh != null;
            }
        }

        private Mesh mesh;
        private JobHandle handle;
        private bool hasPendingJob;
        private NativeArray<int> indices;

        // Reused between builds: a Mesh or a NativeArray created at runtime is never collected by
        // the garbage collector, so neither may be allocated per build.
        private NativeArray<Vector3> vertices;
        private NativeArray<Vector3> normals;
        private int builtRes;

        // Only alive while a build runs (Allocator.TempJob, released as soon as the jobs are done).
        private NativeArray<float> heights;
        private NativeArray<Bounds> boundsData;

        // Named markers: the terrain cost then shows up in the Profiler by name instead of hiding in
        // the collapsed FixedUpdate.ScriptRunBehaviourFixedUpdate summary row.
        private static readonly ProfilerMarker CompleteMarker = new ProfilerMarker("Terrain.CompleteBuild");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("Terrain.UploadMesh");

        // --- Node Tree ---

        /// <summary>
        /// Test whether this node should be splited, or be merged.
        /// </summary>
        /// <param name="cameraPos">Camera Position</param>
        /// <param name="splitDic">LOD Dictionary for Spliting</param>
        /// <param name="mergeDic">LOD Dictionary for Merging</param>
        /// <returns>State code, 0 = do nothing; 1 = split, 2 = merge</returns>
        public int UpdateLod(Vector3 cameraPos, Dictionary<int, float> splitDic, Dictionary<int,float> mergeDic)
        {
            // A Branch node without children has nothing to show. That happens when a stale state was
            // serialised into the scene (or the node list was reset by hand): heal it instead of
            // leaving a permanent hole.
            if (treeState == NodeState.Branch && !HasSubNode) treeState = NodeState.Leaf;

            if (!splitDic.TryGetValue(lodLevel, out float split) ||
                !mergeDic.TryGetValue(lodLevel, out float merge)) return 0;

            var worldBounds = new Bounds(
                new Vector3(size * 0.5f + offset.x, 0, size * 0.5f + offset.y),
                new Vector3(size, 100, size)); // in-dev height data

            float sqr = worldBounds.SqrDistance(cameraPos - FloatingOrigin.origin); // In the bound box -> 0
            bool shouldSplit = sqr < split * split;
            bool shouldMerge = sqr > merge * merge;

            // Should we build the mesh for this node? -> bool hasSubNode ? No : Yes
            if(!HasSubNode && shouldSplit && lodLevel < splitDic.Count - 1)
            {
                treeState = NodeState.Splitting;
                return 1;
            }
            else if(HasSubNode && shouldMerge)
            {
                treeState = NodeState.Merging;
                return 2;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Split this node and build four sub-nodes.
        /// </summary>
        public void BuildSubNodes()
        {
            if(subNodes == null)
            {
                subNodes = new TerrainNode[4];
                int subNodeSize = size / 2;
                for(int i = 0; i < 4; i++)
                {
                    int x = i % 2;
                    int z = i / 2;
                    TerrainNode subNode = new TerrainNode()
                    { 
                        size = subNodeSize, 
                        offset = offset + new Vector2(x * subNodeSize, z * subNodeSize),
                        parentNode = this,
                        lodLevel = lodLevel + 1,
                        res = res
                    };
                    subNodes[i] = subNode;
                }
            }
        }

        public void DisposeSubNodes()
        {
            subNodes = null; // GC will recycle it.
        }

        /// <summary>
        /// Rents a view for this node: a pooled chunk object at this node's position. A hand placed
        /// chunk already owns a scene authored filter and is left exactly as the artist set it - only
        /// the mesh assigned by UploadMesh makes it visible.
        /// </summary>
        public void RentView()
        {
            if (filter != null) return;

            filter = TerrainMeshPool.Pool.Get();
            ownsFilter = true;
            filter.gameObject.transform.localPosition = new Vector3(offset.x, 0, offset.y);
        }

        /// <summary>
        /// Hides this node's view by dropping its mesh: a MeshFilter without a mesh draws nothing, so
        /// neither the GameObject nor its components have to be touched. A pooled view additionally
        /// goes back to the pool; a scene authored one is never deactivated, otherwise the hand placed
        /// terrain would disappear (and stay gone) in the editor.
        /// </summary>
        public void ReleaseView()
        {
            if (filter == null) return;

            filter.sharedMesh = null;

            if (!ownsFilter) return;

            TerrainMeshPool.Pool.Release(filter);
            filter = null;
            ownsFilter = false;
        }

        // --- Mesh Generating ---

        /// <summary>
        /// Dispatches this chunk's jobs. Returns false when the chunk cannot be built.
        /// </summary>
        /// <param name="layers">Noise layers, owned and kept alive by the generator.</param>
        /// <param name="indexBuffer">Index buffer for this chunk's res, shared with every chunk of the same res.</param>
        public bool ScheduleBuild(NativeArray<NoiseLayer> layers, NativeArray<int> indexBuffer)
        {
            if (filter == null)
            {
                Debug.LogWarning("TerrainNode: no MeshFilter assigned, chunk skipped.");
                return false;
            }

            // A pending build owns its buffers, so finish it before reusing them.
            if (hasPendingJob) CompleteBuild();

            int side = Res + 1;
            int vertexCount = side * side;
            int apSide = side + 2; // one vertex apron, so the normals need no extra sampling

            if (!vertices.IsCreated || builtRes != Res)
            {
                DisposeMeshBuffers();
                vertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                normals = new NativeArray<Vector3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                builtRes = Res;
            }

            heights = new NativeArray<float>(apSide * apSide, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            boundsData = new NativeArray<Bounds>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            indices = indexBuffer;

            var heightJob = new HeightFieldJob
            {
                layers = layers,
                originX = offset.x,
                originZ = offset.y,
                spacing = Spacing,
                side = side,
                heights = heights,
            };

            var meshJob = new ChunkMeshJob
            {
                heights = heights,
                spacing = Spacing,
                side = side,
                vertices = vertices,
                normals = normals,
            };

            var boundsJob = new ChunkBoundsJob
            {
                vertices = vertices,
                size = Size,
                bounds = boundsData,
            };

            // 64 items per batch: enough work per batch to hide the dispatch cost, small enough to
            // keep every worker busy.
            JobHandle job = heightJob.Schedule(apSide * apSide, 64);
            job = meshJob.Schedule(vertexCount, 64, job);
            job = boundsJob.Schedule(job);

            handle = job;
            hasPendingJob = true;
            return true;
        }

        /// <summary>Uploads the mesh if the jobs have finished. Returns true when it did.</summary>
        public bool TryComplete()
        {
            if (!hasPendingJob || !handle.IsCompleted) return false;

            CompleteBuild();
            return true;
        }

        /// <summary>Waits for the jobs and uploads the mesh. Does nothing when no build is pending.</summary>
        public void CompleteBuild()
        {
            if (!hasPendingJob) return;

            CompleteMarker.Begin();   // includes the possible main thread stall in handle.Complete()

            handle.Complete();
            hasPendingJob = false;

            UploadMesh();

            if (heights.IsCreated) heights.Dispose();
            if (boundsData.IsCreated) boundsData.Dispose();

            CompleteMarker.End();
        }

        /// <summary>
        /// Sum of every noise layer at a point in noise space, for cpu side queries (the ground
        /// height under the aircraft, for example). The jobs do not use this path.
        /// </summary>
        public float GetHeight(NoiseLayer[] layers, float posX, float posY)
        {
            if (layers == null) return 0f;

            float height = 0f;
            for (int i = 0; i < layers.Length; i++)
            {
                height += layers[i].GetHeight(posX, posY);
            }
            return height;
        }

        /// <summary>
        /// Releases every resource this node owns. Idempotent: calling it twice is safe.
        /// 'indices' points into the generator's shared index buffers and is never disposed here.
        /// </summary>
        public void Dispose()
        {
            if (hasPendingJob) CompleteBuild(); // a job must not outlive the buffers it writes into

            DisposeMeshBuffers();               // vertices / normals
            if (heights.IsCreated) heights.Dispose();
            if (boundsData.IsCreated) boundsData.Dispose();
            indices = default;                  // borrowed, NOT ours to dispose

            // Only what belongs to this node's pooled view is released here. A hand placed chunk is a
            // scene object: its GameObject, its mesh reference and its serialised mesh all survive, so
            // disabling the generator cannot blank the terrain in the editor.
            if (!ownsFilter || !Application.isPlaying) return;

            if (mesh != null)
            {
                if (filter != null) filter.sharedMesh = null;

                UnityEngine.Object.Destroy(mesh);
                mesh = null;
            }

            ReleaseView();
        }

        /// <summary>
        /// Releases this node and its whole subtree, appending every retired node to 'retired' so the
        /// generator can drop them from its node list (a node cannot touch that list itself).
        /// </summary>
        public void ReleaseSubtree(List<TerrainNode> retired)
        {
            if (subNodes != null)
            {
                for (int i = 0; i < subNodes.Length; i++)
                {
                    if (subNodes[i] != null) subNodes[i].ReleaseSubtree(retired);
                }
                subNodes = null;
            }

            Dispose();

            parentNode = null;
            treeState = NodeState.Leaf;
            retired?.Add(this);
        }

        private void UploadMesh()
        {
            UploadMarker.Begin();

            if (mesh == null) mesh = new Mesh { name = "Chunk" };
            mesh.Clear();

            // 16 bit indices wrap past vertex 65535: res 256 means 66049 vertices, and the last rows
            // of the chunk would silently come out missing. Only pay for 32 bit indices when the
            // chunk actually needs them.
            mesh.indexFormat = vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false); // bounds are assigned below
            mesh.bounds = boundsData[0];

            // sharedMesh, not mesh: reading or writing the mesh property instantiates a copy.
            filter.sharedMesh = mesh;

            UploadMarker.End();
        }

        private void DisposeMeshBuffers()
        {
            if (vertices.IsCreated) vertices.Dispose();
            if (normals.IsCreated) normals.Dispose();
            builtRes = 0;
        }
    }
}
