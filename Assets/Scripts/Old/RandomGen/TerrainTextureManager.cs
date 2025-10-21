using UnityEngine;

public class TerrainTextureManager : MonoBehaviour
{
	public Texture2D grassTexture;
	public Texture2D rockTexture;
	public Texture2D sandTexture;

	void Start()
	{
		ApplyTextures();
	}

	void ApplyTextures()
	{
		Terrain terrain = GetComponent<Terrain>();
		TerrainData terrainData = terrain.terrainData;

		TerrainLayer[] terrainLayers = new TerrainLayer[3];

		terrainLayers[0] = new TerrainLayer();
		terrainLayers[0].diffuseTexture = grassTexture;
		terrainLayers[0].tileSize = new Vector2(15, 15);

		terrainLayers[1] = new TerrainLayer();
		terrainLayers[1].diffuseTexture = rockTexture;
		terrainLayers[1].tileSize = new Vector2(15, 15);

		terrainLayers[2] = new TerrainLayer();
		terrainLayers[2].diffuseTexture = sandTexture;
		terrainLayers[2].tileSize = new Vector2(15, 15);

		terrainData.terrainLayers = terrainLayers;

		ApplyTexturesBasedOnHeight();
	}

	void ApplyTexturesBasedOnHeight()
	{
		Terrain terrain = GetComponent<Terrain>();
		TerrainData terrainData = terrain.terrainData;
		float[,,] splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, 3];

		for (int x = 0; x < terrainData.alphamapWidth; x++)
		{
			for (int y = 0; y < terrainData.alphamapHeight; y++)
			{
				float height = terrainData.GetHeight(x, y) / terrainData.size.y;
				float[] splat = new float[3];

				if (height < 0.3f) { splat[2] = 1; } // Sand  
				else if (height < 0.6f) { splat[0] = 1; } // Grass  
				else { splat[1] = 1; } // Rock  

				for (int i = 0; i < 3; i++) splatmapData[x, y, i] = splat[i];
			}
		}

		terrainData.SetAlphamaps(0, 0, splatmapData);
	}

}
