using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The scene's status bars and skills panel. They show the primary selected character.
/// Put this on the same GameObject as SelectionManager.
/// </summary>
[RequireComponent(typeof(SelectionManager))]
public class CharacterHud : MonoBehaviour
{
	private const float BarMax = 100f;

	[SerializeField] private TMP_Text nameText;
	[SerializeField] private Slider hungerSlider;
	[SerializeField] private Slider thirstSlider;
	[SerializeField] private Slider tirednessSlider;
	[SerializeField] private SkillsPanel skillsPanel;

	private SelectionManager selection;
	private Character shown;

	private void Awake()
	{
		selection = GetComponent<SelectionManager>();

		SetMax(hungerSlider);
		SetMax(thirstSlider);
		SetMax(tirednessSlider);
	}

	private void OnEnable() => selection.OnSelectionChanged += HandleSelectionChanged;

	private void OnDisable() => selection.OnSelectionChanged -= HandleSelectionChanged;

	private void HandleSelectionChanged(System.Collections.Generic.IReadOnlyList<PlayerControllable> selected)
	{
		PlayerControllable primary = selection.PrimarySelected;
		Character next = primary != null ? primary.Character : null;
		if (next == shown)
			return;

		shown = next;

		if (skillsPanel == null)
			return;

		if (shown != null)
			skillsPanel.Bind(shown.Skills);
		else
			skillsPanel.Unbind();
	}

	private void Update()
	{
		if (shown == null)
			return;

		if (nameText != null)
			nameText.text = shown.characterName;

		CharacterStats stats = shown.Stats;
		if (stats == null)
			return;

		SetValue(hungerSlider, stats.hunger);
		SetValue(thirstSlider, stats.thirst);
		SetValue(tirednessSlider, stats.tiredness);
	}

	private static void SetMax(Slider slider)
	{
		if (slider != null)
			slider.maxValue = BarMax;
	}

	private static void SetValue(Slider slider, float value)
	{
		if (slider != null)
			slider.value = value;
	}
}
