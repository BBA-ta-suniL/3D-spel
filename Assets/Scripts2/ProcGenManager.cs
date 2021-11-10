using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcGenManager : MonoBehaviour
{
    [SerializeField] ProcGenConfigSO Config;
    [SerializeField] Terrain TargetTerrain;

#if UNITY_EDITOR
    byte[,] BiomeMap_LowResolution;
    float[,] BiomeStrengths_LowResolution;

    byte[,] BiomeMap;
    float[,] BiomeStrengths;
#endif

    private void Start()
    {
        
    }

    private void Update()
    {
        
    }

#if UNITY_EDITOR
    public void RegenerateWorld()
    {
        int mapResolution = TargetTerrain.terrainData.heightmapResolution;
        // generate the low resolution biome map
        Perform_BiomeGeneration_LowResolution((int)Config.BiomeMapResolution);

        // generate the high resolution biome map
        Perform_BiomeGeneration_HighResolution((int)Config.BiomeMapResolution, mapResolution);

        // update the terrain heights
        Perform_HeightMapModification(mapResolution);
    }

    void Perform_BiomeGeneration_LowResolution(int mapResolution)
    {
        BiomeMap_LowResolution = new byte[mapResolution, mapResolution];
        BiomeStrengths_LowResolution = new float[mapResolution, mapResolution];

        int numSeedPoints = Mathf.FloorToInt(mapResolution * mapResolution * Config.BiomeSeedPointDensity);
        List<byte> biomesToSpawn = new List<byte>(numSeedPoints);

        float totalBiomeWeighting = Config.TotalWeighting;
        for (int biomeIndex = 0; biomeIndex < Config.NumBiomes; ++biomeIndex)
        {
            int numEntries = Mathf.RoundToInt(numSeedPoints * Config.Biomes[biomeIndex].Weighting / totalBiomeWeighting);

            for (int entryIndex = 0; entryIndex < numEntries; ++entryIndex)
            {
                biomesToSpawn.Add((byte)biomeIndex);
            }
        }

        while (biomesToSpawn.Count > 0)
        {
            int seedPointIndex = Random.Range(0, biomesToSpawn.Count);
            byte biomeIndex = biomesToSpawn[seedPointIndex];
            
            biomesToSpawn.RemoveAt(seedPointIndex);
            PerformSpawnIndividualBiome(biomeIndex, mapResolution);
        }

        Texture2D biomeMap = new Texture2D(mapResolution, mapResolution, TextureFormat.RGB24, false);
        for (int y = 0; y < mapResolution; ++y)
        {
            for (int x = 0; x < mapResolution; ++x)
            {
                float hue = ((float)BiomeMap_LowResolution[x, y] / (float)Config.NumBiomes);

                biomeMap.SetPixel(x, y, Color.HSVToRGB(hue, 0.75f, 0.75f));
            }
        }
        biomeMap.Apply();

        System.IO.File.WriteAllBytes("BiomeMap_LowResolution.png", biomeMap.EncodeToPNG());
    }

    Vector2Int[] NeighbourOffsets = new Vector2Int[] {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 1),
        new Vector2Int(-1, -1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
    };

    void PerformSpawnIndividualBiome(byte biomeIndex, int mapResolution)
    {
        BiomeConfigSO biomeConfig = Config.Biomes[biomeIndex].Biome;

        Vector2Int spawnLocation = new Vector2Int(Random.Range(0, mapResolution), Random.Range(0, mapResolution));

        float startIntensity = Random.Range(biomeConfig.MinIntensity, biomeConfig.MaxIntensity);

        Queue<Vector2Int> workingList = new Queue<Vector2Int>();
        workingList.Enqueue(spawnLocation);

        bool[,] visited = new bool[mapResolution, mapResolution];
        float[,] targetIntensity = new float[mapResolution, mapResolution];

        targetIntensity[spawnLocation.x, spawnLocation.y] = startIntensity;

        while (workingList.Count > 0)
        {
            Vector2Int workingLocation = workingList.Dequeue();

            BiomeMap_LowResolution[workingLocation.x, workingLocation.y] = biomeIndex;
            visited[workingLocation.x, workingLocation.y] = true;
            BiomeStrengths_LowResolution[workingLocation.x, workingLocation.y] = targetIntensity[workingLocation.x, workingLocation.y];

            for (int neighbourIndex = 0; neighbourIndex < NeighbourOffsets.Length; ++neighbourIndex)
            {
                Vector2Int neighbourLocation = workingLocation + NeighbourOffsets[neighbourIndex];

                if (neighbourLocation.x < 0 || neighbourLocation.y < 0 || neighbourLocation.x >= mapResolution || neighbourLocation.y >= mapResolution)
                    continue;

                if (visited[neighbourLocation.x, neighbourLocation.y])
                    continue;

                visited[neighbourLocation.x, neighbourLocation.y] = true;

                float decayAmount = Random.Range(biomeConfig.MinDecayRate, biomeConfig.MaxDecayRate) * NeighbourOffsets[neighbourIndex].magnitude;
                float neighbourStrength = targetIntensity[workingLocation.x, workingLocation.y] - decayAmount;
                targetIntensity[neighbourLocation.x, neighbourLocation.y] = neighbourStrength;

                if (neighbourStrength <= 0)
                {
                    continue;
                }

                workingList.Enqueue(neighbourLocation);
            }
        }
    }

    byte CalculateHighResBiomeIndex(int lowResMapSize, int lowResX, int lowResY, float fractionX, float fractionY)
    {
        float A = BiomeMap_LowResolution[lowResX, lowResY];
        float B = (lowResX + 1) < lowResMapSize ? BiomeMap_LowResolution[lowResX + 1, lowResY] : A;
        float C = (lowResY + 1) < lowResMapSize ? BiomeMap_LowResolution[lowResX, lowResY + 1] : A;
        float D = 0;

        if ((lowResX + 1) >= lowResMapSize)
            D = C;
        else if ((lowResY + 1) >= lowResMapSize)
            D = B;
        else
            D = BiomeMap_LowResolution[lowResX + 1, lowResY + 1];

        // perform bilinear filtering
        float filteredIndex = A * (1 - fractionX) * (1 - fractionY) + B * fractionX * (1 - fractionY) *
                              C * fractionY * (1 - fractionX) + D * fractionX * fractionY;

        // build an array of the possible biomes based on the values used to interpolate
        float[] candidateBiomes = new float[] { A, B, C, D };

        // find the neighbouring biome closest to the interpolated biome
        float bestBiome = -1f;
        float bestDelta = float.MaxValue;
        for (int biomeIndex = 0; biomeIndex < candidateBiomes.Length; ++biomeIndex)
        {
            float delta = Mathf.Abs(filteredIndex - candidateBiomes[biomeIndex]);

            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestBiome = candidateBiomes[biomeIndex];
            }
        }

        return (byte)Mathf.RoundToInt(bestBiome);
    }

    void Perform_BiomeGeneration_HighResolution(int lowResMapSize, int highResMapSize)
    {
        // allocate the biome map and strength map
        BiomeMap = new byte[highResMapSize, highResMapSize];
        BiomeStrengths = new float[highResMapSize, highResMapSize];

        // calculate map scale
        float mapScale = (float)lowResMapSize / (float)highResMapSize;

        // calculate the high res map
        for (int y = 0; y < highResMapSize; ++y)
        {
            int lowResY = Mathf.FloorToInt(y * mapScale);
            float yFraction = y * mapScale - lowResY;

            for (int x = 0; x < highResMapSize; ++x)
            {
                int lowResX = Mathf.FloorToInt(x * mapScale);
                float xFraction = x * mapScale - lowResX;

                BiomeMap[x, y] = CalculateHighResBiomeIndex(lowResMapSize, lowResX, lowResY, xFraction, yFraction);

                // this would do no interpolation - ie. point based
                //BiomeMap[x, y] = BiomeMap_LowResolution[lowResX, lowResY];
            }
        }

        // save out the biome map
        Texture2D biomeMap = new Texture2D(highResMapSize, highResMapSize, TextureFormat.RGB24, false);
        for (int y = 0; y < highResMapSize; ++y)
        {
            for (int x = 0; x < highResMapSize; ++x)
            {
                float hue = ((float)BiomeMap[x, y] / (float)Config.NumBiomes);

                biomeMap.SetPixel(x, y, Color.HSVToRGB(hue, 0.75f, 0.75f));
            }
        }
        biomeMap.Apply();

        System.IO.File.WriteAllBytes("BiomeMap_HighResolution.png", biomeMap.EncodeToPNG());
    }

    void Perform_HeightMapModification(int mapResolution)
    {
        float[,] heightMap = TargetTerrain.terrainData.GetHeights(0, 0, mapResolution, mapResolution);

        // execute any initial height modifiers
        if (Config.InitialHeightModifier != null)
        {
            BaseHeightMapModifier[] modifiers = Config.InitialHeightModifier.GetComponents<BaseHeightMapModifier>();

            foreach (var modifier in modifiers)
            {
                modifier.Execute(mapResolution, heightMap, TargetTerrain.terrainData.heightmapScale);
            }
        }

        // run heightmap generation for each biome
        for (int biomeIndex = 0; biomeIndex < Config.NumBiomes; ++biomeIndex)
        {
            var biome = Config.Biomes[biomeIndex].Biome;
            if (biome.HeightModifier == null)
                continue;

            BaseHeightMapModifier[] modifiers = biome.HeightModifier.GetComponents<BaseHeightMapModifier>();

            foreach (var modifier in modifiers)
            {
                modifier.Execute(mapResolution, heightMap, TargetTerrain.terrainData.heightmapScale, BiomeMap, biomeIndex, biome);
            }
        }

        // execute any post processing height modifiers
        if (Config.HeightPostProcessingModifier != null)
        {
            BaseHeightMapModifier[] modifiers = Config.HeightPostProcessingModifier.GetComponents<BaseHeightMapModifier>();

            foreach (var modifier in modifiers)
            {
                modifier.Execute(mapResolution, heightMap, TargetTerrain.terrainData.heightmapScale);
            }
        }

        TargetTerrain.terrainData.SetHeights(0, 0, heightMap);
    }
#endif // UNITY_EDITOR
}