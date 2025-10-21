using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
	public int width = 512;
	public int height = 512;
	public float scale = 10f; // Noise-skaalaus

	public Material waterMaterial; // Lis‰‰ vesi-materiaali Inspectorissa

	void Start()
	{
		GenerateTerrain();
		GenerateLakes();
		AddWaterMaterial();
	}

	float FBM(float x, float y, int octaves, float persistence)
	{
		float total = 0;
		float frequency = 1;
		float amplitude = 1;
		float maxValue = 0;

		for (int i = 0; i < octaves; i++)
		{
			total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
			maxValue += amplitude;
			amplitude *= persistence;
			frequency *= 2;
		}

		return total / maxValue;
	}


	void GenerateTerrain()
	{
		Terrain terrain = GetComponent<Terrain>();
		TerrainData terrainData = terrain.terrainData;

		terrainData.heightmapResolution = width + 1;
		terrainData.size = new Vector3(width, 50, height);
		float[,] heights = new float[width, height];

		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				float xCoord = (float)x / width * scale;
				float yCoord = (float)y / height * scale;
				heights[x, y] = FBM(xCoord, yCoord, 5, 0.5f) * 0.5f;

				float ridge = 1.0f - Mathf.Abs(2.0f * Mathf.PerlinNoise(xCoord, yCoord) - 1.0f);
				heights[x, y] = ridge * ridge;

				float warpX = Mathf.PerlinNoise(xCoord * 0.5f, yCoord * 0.5f) * 10f;
				float warpY = Mathf.PerlinNoise(xCoord * 0.5f + 100f, yCoord * 0.5f + 100f) * 10f;
				heights[x, y] = Mathf.PerlinNoise(xCoord + warpX, yCoord + warpY) * 0.5f;

			}
		}

		for (int x = 1; x < width - 1; x++)
		{
			for (int y = 1; y < height - 1; y++)
			{
				float height = heights[x, y];

				// Jos ymp‰rill‰ on matalampi kohta, tasoita
				float minNeighbor = Mathf.Min(heights[x - 1, y], heights[x + 1, y], heights[x, y - 1], heights[x, y + 1]);

				if (height > minNeighbor + 0.01f)
				{
					heights[x, y] -= 0.01f; // "Kuluminen"
				}
			}
		}



		terrainData.SetHeights(0, 0, heights);
	}


	void GenerateLakes()
	{
		Terrain terrain = GetComponent<Terrain>();
		TerrainData terrainData = terrain.terrainData;
		float[,] heights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);

		for (int x = 0; x < terrainData.heightmapResolution; x++)
		{
			for (int y = 0; y < terrainData.heightmapResolution; y++)
			{
				float height = heights[x, y];

				if (height < 0.12f) // Luodaan j‰rvet vain todella mataliin kohtiin
				{
					heights[x, y] = 0.1f; // Tasoitetaan j‰rven pohja
				}
			}
		}

		terrainData.SetHeights(0, 0, heights);
	}

	void AddWaterMaterial()
	{
		Terrain terrain = GetComponent<Terrain>();
		TerrainData terrainData = terrain.terrainData;

		// Selvit‰ Terrainin matalin korkeus
		float minHeight = float.MaxValue;
		float[,] heights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);

		for (int x = 0; x < terrainData.heightmapResolution; x++)
		{
			for (int y = 0; y < terrainData.heightmapResolution; y++)
			{
				if (heights[x, y] < minHeight)
					minHeight = heights[x, y];
			}
		}

		// Skaalaa korkeusmaailmaan
		float waterHeight = minHeight * terrainData.size.y + 2f; // Nostetaan v‰h‰n ylˆs

		// Luo vesitaso
		GameObject waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
		waterPlane.transform.position = new Vector3(width / 2, waterHeight, height / 2);
		waterPlane.transform.localScale = new Vector3(width / 10, 1, height / 10);

		if (waterMaterial != null)
			waterPlane.GetComponent<Renderer>().material = waterMaterial;
	}

}
