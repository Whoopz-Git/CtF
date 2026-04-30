using UnityEngine;

namespace CtF
{
    public static class TerrainSampler
    {
        // Attempts to find the Y value at a given X,Z point on terrain
        public static float SampleTerrain(Vector2 position)
        {
            // If there's a Unity terrain in the scene, sample it
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                return terrain.SampleHeight(new Vector3(position.x, 0, position.y)) + terrain.GetPosition().y;
            }

            // If no terrain, fall back to raycast from above
            if (Physics.Raycast(new Vector3(position.x, 1000f, position.y), Vector3.down, out RaycastHit hitInfo, 2000f))
            {
                return hitInfo.point.y;
            }

            // Default fallback
            CtFLogger.Warn($"Could not find terrain height at ({position.x}, {position.y}). Defaulting to Y = 0.");
            return 0f;
        }
    }
}