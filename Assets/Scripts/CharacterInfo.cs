using UnityEngine;

public class CharacterInfo : MonoBehaviour
{
	public string characterName;
	public int health;
	public int strength;

	// Optional: highlight material
	public Material defaultMat;
	public Material highlightMat;

	private Renderer rend;

	void Start()
	{
		rend = GetComponentInChildren<Renderer>();
		rend.material = defaultMat;
	}

	public void Highlight(bool on)
	{
		if (rend != null)
			rend.material = on ? highlightMat : defaultMat;
	}
}
