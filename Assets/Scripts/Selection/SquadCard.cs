using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One squad member in the squad bar: name, health, hunger and thirst. Click selects them
/// (Shift adds/removes), double-click also moves the camera to them.
/// </summary>
public class SquadCard : MonoBehaviour, IPointerClickHandler
{
	private static readonly Color Frame = new Color(0.16f, 0.13f, 0.10f, 0.92f);
	private static readonly Color SelectedFrame = new Color(0.35f, 1f, 0.45f, 0.95f);
	private static readonly Color Body = new Color(0.23f, 0.19f, 0.15f, 0.95f);
	private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.45f);
	private static readonly Color HealthColor = new Color(0.85f, 0.22f, 0.20f);
	private static readonly Color HungerColor = new Color(0.95f, 0.60f, 0.15f);
	private static readonly Color ThirstColor = new Color(0.25f, 0.55f, 0.95f);

	private PlayerControllable member;
	private Character character;
	private SelectionManager manager;
	private Image frame;
	private Image body;
	private TextMeshProUGUI nameText;
	private RectTransform healthFill;
	private RectTransform hungerFill;
	private RectTransform thirstFill;
	private float nextRefresh;

	public static SquadCard Create(Transform parent, PlayerControllable member, SelectionManager manager, Vector2 topLeft, Vector2 size)
	{
		Character character = member.Character;

		// The bar is anchored at the bottom, so lay cards out from its top-left corner.
		var frame = UIFactory.Panel(character.characterName + " Card", parent, topLeft, size, Frame);
		var body = UIFactory.Panel("Body", frame.transform, new Vector2(2f, 2f), size - new Vector2(4f, 4f), Body, raycast: false);

		var card = frame.gameObject.AddComponent<SquadCard>();
		card.member = member;
		card.character = character;
		card.manager = manager;
		card.frame = frame;
		card.body = body;

		float inner = size.x - 20f;
		card.nameText = UIFactory.Text("Name", body.transform, new Vector2(8f, 4f), new Vector2(inner, 24f), character.characterName, 18f);
		card.nameText.fontStyle = FontStyles.Bold;
		card.healthFill = Bar(body.transform, new Vector2(8f, 32f), new Vector2(inner, 10f), HealthColor);
		card.hungerFill = Bar(body.transform, new Vector2(8f, 47f), new Vector2(inner * 0.5f - 3f, 6f), HungerColor);
		card.thirstFill = Bar(body.transform, new Vector2(8f + inner * 0.5f + 3f, 47f), new Vector2(inner * 0.5f - 3f, 6f), ThirstColor);
		card.Refresh();
		return card;
	}

	/// <summary>A background strip with a fill that is resized by SetFill (no sprite needed).</summary>
	private static RectTransform Bar(Transform parent, Vector2 topLeft, Vector2 size, Color color)
	{
		var back = UIFactory.Panel("Bar", parent, topLeft, size, BarBack, raycast: false);
		var fill = UIFactory.Panel("Fill", back.transform, Vector2.zero, size, color, raycast: false).rectTransform;
		fill.anchorMin = Vector2.zero;
		fill.anchorMax = Vector2.one;
		fill.offsetMin = fill.offsetMax = Vector2.zero;
		return fill;
	}

	private static void SetFill(RectTransform fill, float value)
	{
		fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
	}

	private void Update()
	{
		if (Time.unscaledTime >= nextRefresh)
		{
			nextRefresh = Time.unscaledTime + 0.25f;
			Refresh();
		}
	}

	public void Refresh()
	{
		if (character == null)
		{
			gameObject.SetActive(false);
			return;
		}

		var stats = character.Stats;
		bool alive = character.IsAlive;
		bool selected = alive && manager != null && manager.selectedCharacters.Contains(member);

		frame.color = selected ? SelectedFrame : Frame;
		body.color = alive ? Body : new Color(0.12f, 0.12f, 0.12f, 0.9f);
		nameText.text = alive ? character.characterName : $"{character.characterName} (dead)";
		nameText.color = alive ? UIFactory.TextColor : UIFactory.MutedTextColor;

		if (stats != null)
		{
			SetFill(healthFill, stats.currentHealth / Mathf.Max(1f, stats.maxHealth));
			SetFill(hungerFill, stats.hunger / 100f);
			SetFill(thirstFill, stats.thirst / 100f);
		}
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData.button != PointerEventData.InputButton.Left || manager == null || !character.IsAlive)
			return;

		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		if (eventData.clickCount >= 2)
		{
			manager.SelectOnly(member);
			var cam = Camera.main != null ? Camera.main.GetComponent<RTSCamera>() : null;
			if (cam != null)
				cam.FocusOn(character.transform.position);
		}
		else if (shift)
			manager.Toggle(member);
		else
			manager.SelectOnly(member);
	}
}
