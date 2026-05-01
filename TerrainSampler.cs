using UnityEngine;

namespace CtF
{
    public static class TerrainSampler
    {
        public static float SampleTerrain(Vector2 position)
        {
            var terrains = Terrain.activeTerrains;

            if (terrains != null && terrains.Length > 0)
            {
                float highestY = float.MinValue;

                foreach (var terrain in terrains)
                {
                    if (terrain == null) continue;

                    float terrainPos = terrain.GetPosition().y;
                    float sampledY = terrain.SampleHeight(new Vector3(position.x, 0, position.y)) + terrainPos;

                    if (sampledY > highestY)
                        highestY = sampledY;
                }

                return highestY;
            }

            // Fallback to raycast from above
            if (Physics.Raycast(new Vector3(position.x, 1000f, position.y), Vector3.down, out RaycastHit hitInfo, 2000f))
                return hitInfo.point.y;

            CtFLogger.Warn($"Could not find terrain height at ({position.x}, {position.y}). Defaulting to Y = 0.");
            return 0f;
        }
    }
}