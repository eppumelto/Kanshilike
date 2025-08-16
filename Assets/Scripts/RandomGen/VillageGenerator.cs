using System.Collections.Generic;
using UnityEngine;

public class VillageGenerator : MonoBehaviour
{
	public GameObject villagePrefab; // Ved‰ kyl‰-prefab Unityss‰ t‰h‰n
	public Terrain terrain;
	public int villageCount = 10; // Kuinka monta kyl‰‰ lis‰t‰‰n
	public float minVillageHeight = 0.15f; // V‰himm‰iskorkeus kyl‰lle
	public float maxVillageHeight = 0.6f; // Maksimikorkeus kyl‰lle
	public float maxSlope = 0.1f; // Kuinka jyrkk‰ maasto saa olla kyl‰n alla

	void Start()
	{
		GenerateVillages();
	}

	void GenerateVillages()
	{
		TerrainData terrainData = terrain.terrainData;
		int terrainWidth = terrainData.heightmapResolution;
		int terrainHeight = terrainData.heightmapResolution;
		float[,] heights = terrainData.GetHeights(0, 0, terrainWidth, terrainHeight);

		int villagesPlaced = 0;
		int attempts = 0;
		int maxAttempts = 500;

		while (villagesPlaced < villageCount && attempts < maxAttempts)
		{
			attempts++;

			// Satunnainen paikka
			int x = Random.Range(10, terrainWidth - 10);
			int y = Random.Range(10, terrainHeight - 10);

			float height = heights[x, y];

			// Kyl‰n korkeus tarkistus
			if (height < minVillageHeight || height > maxVillageHeight)
				continue;

			// Tarkistetaan, ett‰ alue on tasainen
			if (IsFlatEnough(x, y, heights, terrainWidth, terrainHeight))
			{
				// Muunnetaan heightmapin koordinaatit Unityn maailmakoordinaateiksi
				Vector3 worldPos = new Vector3(
					x / (float)terrainWidth * terrainData.size.x,
					0, // Korkeus lasketaan seuraavaksi
					y / (float)terrainHeight * terrainData.size.z
				);

				// Selvitet‰‰n oikea korkeus Terrainilta
				float worldHeight = terrain.SampleHeight(worldPos);

				// Lis‰t‰‰n offset, jotta kyl‰ ei ole maan sis‰ss‰
				float heightOffset = 0.2f; // Kokeile eri arvoja tarvittaessa

				Vector3 finalPosition = new Vector3(worldPos.x, worldHeight + heightOffset, worldPos.z);

				Instantiate(villagePrefab, finalPosition, Quaternion.identity);

				villagesPlaced++;
			}
		}

		Debug.Log($"Placed {villagesPlaced} villages after {attempts} attempts.");
	}


	bool IsFlatEnough(int x, int y, float[,] heights, int width, int height)
	{
		float centerHeight = heights[x, y];
		float maxDifference = 0f;

		// Tarkistetaan korkeuserot ymp‰rˆivien pisteiden kanssa
		for (int dx = -2; dx <= 2; dx++)
		{
			for (int dy = -2; dy <= 2; dy++)
			{
				int nx = Mathf.Clamp(x + dx, 0, width - 1);
				int ny = Mathf.Clamp(y + dy, 0, height - 1);
				float diff = Mathf.Abs(centerHeight - heights[nx, ny]);
				if (diff > maxSlope)
					return false;
			}
		}
		return true;
	}
}
