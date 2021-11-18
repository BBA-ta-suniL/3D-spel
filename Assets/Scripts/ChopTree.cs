using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChopTree : MonoBehaviour
{
	public Camera FPSCamera;
	public float hitRange = 2.5f;
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{
		Ray ray = FPSCamera.ScreenPointToRay(new Vector2(Screen.width / 2, Screen.height / 2));
		RaycastHit hitInfo;

		if (Input.GetKeyDown(KeyCode.Mouse0))
		{
			if (Physics.Raycast(ray, out hitInfo, hitRange))
			{
				Debug.Log(hitInfo);
			}
		}
	}
}
