using UnityEngine;

/// <summary>
/// A flat ring on the ground under a character: solid green when selected, faint white while a
/// drag-box is about to select it. Built from a LineRenderer, so no art is needed.
/// </summary>
[DisallowMultipleComponent]
public class SelectionRing : MonoBehaviour
{
	private static readonly Color SelectedColor = new Color(0.35f, 1f, 0.45f, 0.95f);
	private static readonly Color PreviewColor = new Color(1f, 1f, 1f, 0.55f);
	private const int Segments = 48;
	private const float Width = 0.07f;

	private static Material sharedMaterial;

	private LineRenderer line;
	private bool selected;
	private bool preview;

	private void Awake()
	{
		Build();
		Refresh();
	}

	public void SetSelected(bool value)
	{
		selected = value;
		Refresh();
	}

	public void SetPreview(bool value)
	{
		preview = value;
		Refresh();
	}

	private void Refresh()
	{
		if (line == null)
			return;

		line.enabled = selected || preview;
		Color color = selected ? SelectedColor : PreviewColor;
		line.startColor = line.endColor = color;
	}

	private void Build()
	{
		// Size and height from the character's collider (falls back to a 1 m wide, 2 m tall body).
		var col = GetComponentInChildren<Collider>();
		Bounds bounds = col != null ? col.bounds : new Bounds(transform.position, new Vector3(1f, 2f, 1f));
		float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.15f;
		float feet = (bounds.min.y - transform.position.y) / Mathf.Max(0.01f, transform.lossyScale.y);

		var ring = new GameObject("SelectionRing").transform;
		ring.SetParent(transform, false);
		ring.localPosition = new Vector3(0f, feet + 0.05f, 0f);
		// Lie flat: the ring is drawn in local XY and faces along local Z, which now points up.
		ring.localRotation = Quaternion.Euler(90f, 0f, 0f);

		line = ring.gameObject.AddComponent<LineRenderer>();
		line.useWorldSpace = false;
		line.loop = true;
		line.alignment = LineAlignment.TransformZ;
		line.widthMultiplier = Width;
		line.numCornerVertices = 2;
		line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		line.receiveShadows = false;
		line.sharedMaterial = SharedMaterial();

		line.positionCount = Segments;
		float scale = 1f / Mathf.Max(0.01f, transform.lossyScale.x);
		for (int i = 0; i < Segments; i++)
		{
			float a = i * Mathf.PI * 2f / Segments;
			line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius * scale);
		}
	}

	private static Material SharedMaterial()
	{
		if (sharedMaterial == null)
		{
			Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
			sharedMaterial = new Material(shader) { name = "SelectionRing" };
		}
		return sharedMaterial;
	}
}
