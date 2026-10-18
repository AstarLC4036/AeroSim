using UnityEngine;

namespace AeroSim.Terrain
{
    /// <summary>
    /// Points the terrain generator at one baked DEM dataset.
    /// <para>The data itself deliberately lives outside the asset pipeline:
    /// <c>Assets/StreamingAssets/&lt;folder&gt;/world.json</c> plus <c>level/NN.r16</c>, written by
    /// <c>Tools/dem_import.py</c>. Nothing is imported as a texture, so texture compression, the sRGB
    /// decode and the max size clamp cannot touch the heights.</para>
    /// <para>Assign it on the TerrainGenerator and the chunks are built from real elevation. Leave the
    /// slot empty and the noise layers stay in charge.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "DemAsset", menuName = "AeroSim/Terrain/Dem Asset")]
    public class DemAsset : ScriptableObject
    {
        [Tooltip("Folder under Assets/StreamingAssets holding world.json and level/.")]
        public string folder = "dem/everest";

        [Tooltip("World position in metres (X east, Z south) of the dataset's north-west corner.")]
        public Vector2 worldOrigin = Vector2.zero;

        [Tooltip("Added to every sampled height. Use minus the base height from world.json to bring the " +
                 "lowest point of the dataset down to y = 0.")]
        public float verticalOffset;
    }
}
