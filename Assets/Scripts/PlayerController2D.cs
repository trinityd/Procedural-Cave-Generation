using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController2D : MonoBehaviour {

	Rigidbody2D rb;
	Vector2 vel;

	// Use this for initialization
	void Start () {
		rb = GetComponent<Rigidbody2D>();
	}
	
	// Update is called once per frame
	void Update () {
		vel = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized * 10;
	}

	void FixedUpdate() {
		rb.MovePosition(rb.position + vel * Time.fixedDeltaTime);
	}
}
