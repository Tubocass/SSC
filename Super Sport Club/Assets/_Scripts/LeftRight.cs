﻿using UnityEngine;
using System.Collections;

public class LeftRight : MonoBehaviour {

	public float speed;
	Animator anim;
	// Use this for initialization
	void Start () 
	{
		anim = GetComponent<Animator> ();
	}
	
	// Update is called once per frame
	void Update () 
	{
		float h = Input.GetAxisRaw ("Horizontal");
		anim.SetInteger("PanDir",(int) h);
//		if (h != 0) 
//		{
//			Vector3 targetDir = new Vector3 (speed * h, 0, 0);
//			targetDir  = tran.position + targetDir *Time.deltaTime; 
//			tran.position = Vector3.MoveTowards(tran.position,targetDir,0.25f);
//
//		}
	}
}
