using UnityEngine;

public class RTSCamera : MonoBehaviour
{
	public float panSpeed = 20f;
	public float rotateSpeed = 50f;
	public float zoomSpeed = 500f;

	void Update()
	{
		// Ctrl+key are shortcuts (Ctrl+A selects all), not camera movement.
		if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
			return;

		// Panning (relative to camera's horizontal facing direction)
		Vector3 forward = transform.forward;
		Vector3 right = transform.right;
		forward.y = 0f;
		right.y = 0f;
		forward.Normalize();
		right.Normalize();

		if (Input.GetKey(KeyCode.W)) transform.Translate(forward * panSpeed * Time.deltaTime, Space.World);
		if (Input.GetKey(KeyCode.S)) transform.Translate(-forward * panSpeed * Time.deltaTime, Space.World);
		if (Input.GetKey(KeyCode.A)) transform.Translate(-right * panSpeed * Time.deltaTime, Space.World);
		if (Input.GetKey(KeyCode.D)) transform.Translate(right * panSpeed * Time.deltaTime, Space.World);

		// Rotation
		if (Input.GetKey(KeyCode.Q)) transform.Rotate(Vector3.up, -rotateSpeed * Time.deltaTime, Space.World);
		if (Input.GetKey(KeyCode.E)) transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

		// Zoom
		float scroll = Input.GetAxis("Mouse ScrollWheel");
		transform.Translate(Vector3.forward * scroll * zoomSpeed * Time.deltaTime, Space.Self);
	}

	/// <summary>Slides the camera sideways (same height, angle and zoom) so it looks at <paramref name="target"/>.</summary>
	public void FocusOn(Vector3 target)
	{
		// Where the view centre currently meets the target's height.
		var plane = new Plane(Vector3.up, target);
		var ray = new Ray(transform.position, transform.forward);
		if (!plane.Raycast(ray, out float distance))
			return;

		Vector3 offset = target - ray.GetPoint(distance);
		offset.y = 0f;
		transform.position += offset;
	}
}
