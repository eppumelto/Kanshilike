using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shows a game-over overlay with a Restart button once every player character is dead.
/// Put one in each playable scene.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private string message = "Your squad has fallen.";
    [Tooltip("Seconds between the last death and the overlay appearing, so the fall can be seen.")]
    [SerializeField, Min(0f)] private float delay = 1.5f;

    private readonly List<CharacterStats> squad = new List<CharacterStats>();
    private float showAt = -1f;
    private GUIStyle titleStyle;

    public bool IsGameOver => showAt >= 0f;

    private void Start()
    {
        foreach (var character in FindObjectsByType<SelectableCharacter>(FindObjectsSortMode.None))
        {
            var stats = character.GetComponent<CharacterStats>();
            if (stats == null)
                continue;

            squad.Add(stats);
            stats.OnDied += HandleMemberDied;
        }
    }

    private void OnDestroy()
    {
        foreach (var stats in squad)
            if (stats != null)
                stats.OnDied -= HandleMemberDied;
    }

    private void HandleMemberDied(GameObject killer)
    {
        if (IsGameOver)
            return;

        foreach (var stats in squad)
            if (stats != null && stats.IsAlive)
                return;

        showAt = Time.unscaledTime + delay;
    }

    private void OnGUI()
    {
        if (!IsGameOver || Time.unscaledTime < showAt)
            return;

        titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 36,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        GUI.Label(new Rect(0, cy - 80, Screen.width, 60), message, titleStyle);

        if (GUI.Button(new Rect(cx - 80, cy, 160, 40), "Restart"))
            Restart();
    }

    private static void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // Works in the editor even if the scene is not in Build Settings.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex);
#endif
    }
}
