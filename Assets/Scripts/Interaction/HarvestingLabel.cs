using UnityEngine;
using TMPro;

/// <summary>
/// Generic action label shown above a character during timed interactions
/// (e.g. "Harvesting...", "Drinking...").
/// Uses a 3D TextMeshPro — NO Canvas needed.
/// SelectableCharacter instantiates, calls SetText(), and destroys it automatically.
///
/// Prefab setup:
///   1. Create an empty GameObject.
///   2. Add a TextMeshPro component (3D, not TextMeshProUGUI).
///   3. Set font size ~2, color white, center-aligned. Leave default text empty.
///   4. Add this component — it auto-finds the TextMeshPro if left unassigned.
///   5. Save as prefab and assign to SelectableCharacter.harvestingLabelPrefab.
/// </summary>
public class HarvestingLabel : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;

    void Awake()
    {
        if (label == null)
            label = GetComponent<TextMeshPro>();
    }

    /// <summary>Set the text shown on the label (e.g. "Harvesting...", "Drinking...").</summary>
    public void SetText(string text)
    {
        if (label != null)
            label.text = text;
    }

    void LateUpdate()
    {
        // Billboard: always face the main camera so the text is readable from any angle.
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;
    }
}

