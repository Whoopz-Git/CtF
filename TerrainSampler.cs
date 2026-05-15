using UnityEngine;

namespace CtF
{
    public static class TerrainSampler
    {
        public static float SampleTerrain(Vector2 position)
        {
            var terrains = Terrain.activeTerrains;
            float terrainY = 0f;

            if (terrains != null && terrains.Length > 0)
            {
                float highestY = float.MinValue;

                foreach (var terrain in terrains)
                {
                    if (terrain == null) continue;

                    float sampledY = terrain.SampleHeight(new Vector3(position.x, 0, position.y)) + terrain.GetPosition().y;

                    if (sampledY > highestY)
                        highestY = sampledY;
                }

                terrainY = highestY;
            }

            float rayOriginY = terrainY + 20f;

            if (Physics.Raycast(new Vector3(position.x, rayOriginY, position.y), Vector3.down, out RaycastHit hitInfo, 40f))
                return hitInfo.point.y;

            CtFLogger.Warn($"Could not find surface height at ({position.x}, {position.y}). Falling back to terrain height.");
            return terrainY;
        }
    }
}