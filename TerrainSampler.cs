using UnityEngine;

namespace CtF
{
    public static class TerrainSampler
    {
        public static float SampleTerrain(Vector2 position)
        {
            if (Physics.Raycast(new Vector3(position.x, 1000f, position.y), Vector3.down, out RaycastHit hitInfo, 2000f))
                return hitInfo.point.y;

            CtFLogger.Warn($"Could not find terrain height at ({position.x}, {position.y}). Defaulting to Y = 0.");
            return 0f;
        }
    }
}