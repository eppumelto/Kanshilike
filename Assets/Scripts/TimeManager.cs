using UnityEngine;
using System;

public class TimeManager : MonoBehaviour
{
	[Header("Time Settings")]
	public float timeMultiplier = 60f; // 1 real sec = 1 in-game minute
	public float startTime = 8f; // start at 8:00 AM
	[Range(0, 24)] public float currentTime; // current time of day in hours

	[Header("Sun Settings")]
	public Light sun;
	public Gradient ambientColor;
	public AnimationCurve sunIntensity;

	public event Action<float> OnTimeChanged; // broadcasts current time (0–24)

	private void Start()
	{
		currentTime = startTime;
	}

	private void Update()
	{
		UpdateTime();
		RotateSun();
		UpdateLighting();
	}

	void UpdateTime()
	{
		currentTime += Time.deltaTime * (timeMultiplier / 60f); // convert to hours
		if (currentTime >= 24f) currentTime = 0f;

		OnTimeChanged?.Invoke(currentTime);
	}

	void RotateSun()
	{
		float sunAngle = (currentTime / 24f) * 360f - 90f;
		sun.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0);
	}

	void UpdateLighting()
	{
		RenderSettings.ambientLight = ambientColor.Evaluate(currentTime / 24f);
		sun.intensity = sunIntensity.Evaluate(currentTime / 24f);
	}
}
