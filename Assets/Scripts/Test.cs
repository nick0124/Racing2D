using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour {

    public Vector2 vel;
    public float angVel;
    public GameObject wheel;

    public float timer;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        vel = gameObject.GetComponent<Rigidbody2D>().velocity;
        angVel = wheel.GetComponent<Rigidbody2D>().angularVelocity;
        timer += Time.deltaTime;
	}
}
