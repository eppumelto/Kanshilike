using UnityEngine;

public class RTSCamera : MonoBehaviour
{
	public float panSpeed = 20f;
	public float rotateSpeed = 50f;
	public float zoomSpeed = 500f;

	void Update()
	{
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
}
