using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Pool;

namespace AeroSim.Terrain
{
    public class TerrainMeshPool : MonoBehaviour
    {
        private static TerrainMeshPool instance;
        public static TerrainMeshPool Instance => instance;

        public ObjectPool<MeshFilter> terrainMeshPool;
        public Transform meshParent;
        public Material terrainMat;

        public static ObjectPool<MeshFilter> Pool => instance.terrainMeshPool;

        public void Awake()
        {
            instance = this;
            // maxSize caps how many chunk views (each one holds a Mesh) may sit idle in the pool.
            terrainMeshPool = new ObjectPool<MeshFilter>(CreateTerrain, OnGetTerrain, OnReleaseTerrain, OnDestroyTerrain, true, 32, 128);
        }

        public MeshFilter CreateTerrain()
        {
            GameObject newObj = new GameObject("TerrainChunk");
            newObj.transform.parent = meshParent;
            MeshFilter filter = newObj.AddComponent<MeshFilter>();
            MeshRenderer renderer = newObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMat;
            return filter;
        }

        public void OnGetTerrain(MeshFilter filter)
        {
            filter.gameObject.SetActive(true);
        }

        public void OnReleaseTerrain(MeshFilter filter)
        {
            filter.sharedMesh = null;
            filter.gameObject.SetActive(false);
        }

        public void OnDestroyTerrain(MeshFilter filter)
        {
            Destroy(filter.gameObject);
        }
    }
}
