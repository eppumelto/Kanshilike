using UnityEngine;

public class RTSCamera : MonoBehaviour
{
	public float panSpeed = 20f;
	public float rotateSpeed = 50f;
	public float zoomSpeed = 500f;

	void Update()
	{
		// Panning
		if (Input.GetKey(KeyCode.W)) transform.Translate(Vector3.forward * panSpeed * Time.deltaTime, Space.World);
		if (Input.GetKey(KeyCode.S)) transform.Translate(Vector3.back * panSpeed * Time.deltaTime, Space.World);
		if (Input.GetKey(KeyCode.A)) transform.Translate(Vector3.left * panSpeed * Time.deltaTime, Space.World);
		if (Input.GetKey(KeyCode.D)) transform.Translate(Vector3.right * panSpeed * Time.deltaTime, Space.World);

		// Rotation
		if (Input.GetKey(KeyCode.Q)) transform.Rotate(Vector3.up, -rotateSpeed * Time.deltaTime, Space.World);
		if (Input.GetKey(KeyCode.E)) transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

		// Zoom
		float scroll = Input.GetAxis("Mouse ScrollWheel");
		transform.Translate(Vector3.forward * scroll * zoomSpeed * Time.deltaTime, Space.Self);
	}
}
