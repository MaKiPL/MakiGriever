using UnityEngine;
using System.Collections;

public class PlayerMoveController : MonoBehaviour {

	//I've modified it a bit - to prevent the rigidbody forced controller. It's now all camera only 
	// PUBLIC
	public SimpleTouchController leftController;
	public SimpleTouchController rightController;
	public Transform headTrans;
	public float speedMovements = 5f;
	//public float speedContinuousLook = 100f;
	public float speedProgressiveLook = 3000f;

	// PRIVATE
	[SerializeField] bool continuousRightController = true;
	private Vector3 currentRotation;
	private Vector2 keyboardInput;

	void Awake()
	{
		rightController.TouchEvent += RightController_TouchEvent;
		currentRotation = transform.eulerAngles;
	}

	public bool ContinuousRightController
	{
		set{continuousRightController = value;}
	}

	void RightController_TouchEvent (Vector2 value)
	{
		if(!continuousRightController)
		{
			UpdateAim(value);
		}
	}

	void Update()
	{
		keyboardInput.x = Input.GetAxis("Horizontal"); // A & D keys
		keyboardInput.y = Input.GetAxis("Vertical");   // W & S keys
		// move
		// Combine touch/keyboard movement
		Vector2 movementInput = leftController.GetTouchPosition + keyboardInput;

		// Move camera position
		transform.position += transform.forward * (movementInput.y * Time.deltaTime * speedMovements) +
		                      transform.right * (movementInput.x * Time.deltaTime * speedMovements);


		
		if(continuousRightController)
		{
			UpdateAim(rightController.GetTouchPosition);
		}
	}

	void UpdateAim(Vector2 value)
	{
		if (headTrans != null)
		{
			// Update camera rotation with head transform
			currentRotation.y += -value.x * Time.deltaTime * speedProgressiveLook;
			transform.rotation = Quaternion.Euler(0f, currentRotation.y, 0f);

			Vector3 headRotation = headTrans.localEulerAngles;
			headRotation.x -= value.y * Time.deltaTime * speedProgressiveLook;
            
			// Clamp vertical rotation to avoid over-rotation
			if (headRotation.x > 180f) headRotation.x -= 360f;
			headRotation.x = Mathf.Clamp(headRotation.x, -80f, 80f);
            
			headTrans.localRotation = Quaternion.Euler(headRotation);
		}
		else
		{
			// Direct camera rotation without head transform
			currentRotation.x -= value.y * Time.deltaTime * speedProgressiveLook;
			currentRotation.y += value.x * Time.deltaTime * speedProgressiveLook;
            
			// Clamp vertical rotation
			if (currentRotation.x > 180f) currentRotation.x -= 360f;
			currentRotation.x = Mathf.Clamp(currentRotation.x, -80f, 80f);
            
			transform.rotation = Quaternion.Euler(currentRotation);
		}
	}

	void OnDestroy()
	{
		rightController.TouchEvent -= RightController_TouchEvent;
	}

}
