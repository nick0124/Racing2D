using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarMove : MonoBehaviour {

    public List<GameObject> wheels;
    public float maxTorque;
    public float acceleration;
    public float curTorque = 0;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {

        float startVel = 0;

        if (Input.GetKeyDown(KeyCode.D) && Input.GetKeyDown(KeyCode.A))
        {
            foreach (var wheel in wheels)
            {
                curTorque = wheel.GetComponent<Rigidbody2D>().angularVelocity;
                //startVel = wheel.GetComponent<Rigidbody2D>().angularVelocity;
            }
        }

		if(Input.GetKey(KeyCode.D))
        {
            if (curTorque < maxTorque)
            {
                curTorque += acceleration;
            }

            foreach (var wheel in wheels)
            {
                wheel.GetComponent<Rigidbody2D>().angularVelocity = -curTorque;//AddTorque(-curTorque);
            }
        }
        else if (Input.GetKey(KeyCode.A))
        {
            if (curTorque > -maxTorque)
            {
                curTorque -= acceleration;
            }

            foreach (var wheel in wheels)
            {
                wheel.GetComponent<Rigidbody2D>().angularVelocity = -curTorque;//AddTorque(-curTorque);
            }
        }
        else
        {
            

            foreach (var wheel in wheels)
            {
                curTorque = -wheel.GetComponent<Rigidbody2D>().angularVelocity;
                wheel.GetComponent<Rigidbody2D>().angularVelocity = wheel.GetComponent<Rigidbody2D>().angularVelocity;//AddTorque(-curTorque);
            }
        }

        
            

	}
}
