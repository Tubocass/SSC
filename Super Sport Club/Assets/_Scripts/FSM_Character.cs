﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Hashtable = ExitGames.Client.Photon.Hashtable;


public class FSM_Character : FSM_Base 
{
	public int id, actionCount, targetCount, maxActions = 2;
	public Team.TeamNumber team;
	public bool AmTeamOne{get{return (int)team == 0;}}
	public Vector3 newLocation = Vector3.zero;
	public CharacterData charData; // contains Name, Id, and stats
	public bool hasTarget, hasBall;
	public Cell OccupiedCell
	{
		get
		{
			return Grid_Setup.Instance.GetCellByLocation(Location);
		}
	}
	public Cell LastTargetCell
	{
		get{return lastCell;}
	}
	public Vector3 Location
	{
		get{return tran.position;}
	}
	public enum Stance
	{
		Neutral,
		Move,
		Sprint,
		Defend,
		Pass,
		Shoot,
		Cover_Man,
		Cover_Ball
	};
	protected string easeType;
	[SerializeField] protected iTween.EaseType ease, ballEase;
	[SerializeField] LayerMask characterLayer;
	[SerializeField] GameObject destPin;
	[SerializeField] Color teamColor;
	[SerializeField] float rayLength = 1.5f;
	BallScript ball;
	GameObject[] targetPins;
	GameObject passTargetPin;//this could be put with targetPins[] and just given a different color
	PlayerAction[] actions;
	Cell lastCell;
	MeshRenderer currentMesh;
	//Animator anim;
	Transform tran;
	FSM_Character opp;
	Vector3 offset = new Vector3(0,0.2f,0);

	public void ReturnCharacter(Hashtable ht)
	{
		CharacterData cd = ScriptableObject.CreateInstance<CharacterData>();
		this.id = (int)ht["ID"];
		cd.name = (string)ht["Name"];
		cd.Strength = (float)ht["Strength"];
		cd.Speed = (float)ht["Speed"];
		cd.Defense = (float)ht["Defense"];
		this.charData = cd;
		this.team = (Team.TeamNumber)ht["Team"];
		this.newLocation = (Vector3)ht["Location"];
	}

	void Awake()
	{
		CurrentState = Stance.Neutral;
		tran = transform;
		currentMesh = GetComponentInChildren<MeshRenderer>();
		//anim = GetComponentInChildren<Animator>();
		actions = new PlayerAction[maxActions];
		targetPins = new GameObject[maxActions];
		passTargetPin = Instantiate(destPin,Vector3.zero,Quaternion.identity) as GameObject;
		passTargetPin.SetActive(false);
	}
	void Update()
	{
		Debug.DrawRay(tran.position,tran.forward);
	}

	public void SetPlayerAction(PlayerAction act)
	{
		if(actionCount<maxActions)
		{
			actions[actionCount] = act;
			actionCount += 1;
		}
	}

	public void SetMoveTarget(Cell target)
	{
		targetPins[targetCount] = Instantiate(destPin, target.Location, Quaternion.identity) as GameObject;
		targetCount++;
		lastCell = target;
	}
	public void MoveTransform(Vector3 newLoc)
	{
		tran.position = newLoc;
	}
	public void SetPassTarget(Cell target)
	{
		passTargetPin.SetActive(true);
		passTargetPin.transform.position = target.Location;
	}

	public Hashtable GetCharacterAsProp()
	{
		Hashtable ht = new Hashtable();
		ht["Location"] = Location;
		ht["Name"] = charData.name;
		ht["ID"] = id;
		ht["Strength"] = charData.Strength;
		ht["Speed"] = charData.Speed;
		ht["Defense"] = charData.Defense;
		ht["Team"] = (int)team;
		return ht;
	}

	public void ClearActions()
	{
		for(int c= 0;c<actions.Length;c++)
		{
			actions[c] = null;
		}
		actionCount = 0;
		for(int t= 0;t<targetPins.Length;t++)
		{
			if(targetPins[t]!=null)
			Destroy(targetPins[t]);
		}
		targetCount = 0;
		passTargetPin.SetActive(false);
		lastCell = null;
	}

	public IEnumerator ExecuteActions()
	{
		for (int i = 0;i<actions.Length;i++)
		{
			if(actions[i]!=null)
			{
				switch(actions[i].action)
				{
					case PlayerAction.Actions.Move:
					{
						Vector3 target = actions[i].cTo.Location;
						target += offset;
						RotateTowards(target);
					
						easeType = ease.ToString();
						Vector3 dir, nextCell;
						while (Vector3.Distance(tran.position,target)>.1f) 
						{
							dir = target - (OccupiedCell.Location + offset);
							nextCell = (OccupiedCell.Location + offset) + dir.normalized;
							if(CanMove(actions[i].cTo))
							{
								iTween.MoveTo(gameObject, iTween.Hash("position", nextCell, "easeType", easeType, "loopType", "none", "speed", charData.Speed));

								yield return new WaitForSeconds(0.2f);
							}else break;
						}
						break;
					}
					case PlayerAction.Actions.Pass:
					{
						if(hasBall)
						{
							Hashtable ht = new Hashtable();
							ht["Speed"] = (float)charData.Strength;
							ht["Cell"] = actions[i].cTo.id;
							ht["EaseType"] = ballEase.ToString();
							ball.StartCoroutine("MoveTo",ht);
						}
						break;
					}
					case PlayerAction.Actions.Shoot:
					{
						if(hasBall)
						{
							Hashtable ht = new Hashtable();
							ht["Speed"] = (float)charData.Strength;
							ht["Cell"] = actions[i].cTo.id;
							ht["EaseType"] = ballEase.ToString();
							ball.StartCoroutine("MoveTo",ht);
							UnityEventManager.TriggerEvent("ShotFired");
						}
						break;
					}
				}
			}else break;
		}
		ClearActions();	
		yield return null;
	}
	void RotateTowards(Vector3 target)// this is really a SetRotation function
	{
		Vector3 dir = target - tran.position;
		Quaternion rotation = Quaternion.LookRotation(dir);
		tran.rotation= rotation;
		//float f = Vector3.Angle(tran.forward,dir);
		//tran.Rotate(Vector3.up,f);
	}

	FSM_Character PlayerInFrontOfMe()
	{
		Ray ray = new Ray(tran.position,tran.forward);
		RaycastHit hit = new RaycastHit();;
		Physics.Raycast(ray,out hit,rayLength,characterLayer);
		if(hit.collider!=null&& hit.collider!= this.GetComponent<Collider>() && hit.transform.tag == "Player")
		{
			return hit.transform.GetComponent<FSM_Character>();
		}
		return null;
	}
	bool CanMove(Cell targetCell)
	{
		bool canMove;
		opp = PlayerInFrontOfMe();
		if(opp!=null)
		{
			Debug.Log("Player ID: "+opp.id);
			if(targetCell.Location== opp.transform.position)
			{
				if(targetCell==opp.LastTargetCell)
				{
					canMove = false;
				}else{
					canMove = true;
				}
			}else{
				float dotFace = Vector3.Dot(tran.forward,opp.transform.forward);
				if(dotFace<0)
				{
					Debug.Log("Oh, just kiss already");
					canMove = false;
				}else{
					canMove = true;
				}
			}
		}else canMove = true;
		return canMove;//(!Physics.CheckSphere(Location+tran.forward, 1, characterLayer));
		 
	}

	public void Highlight(bool set)
	{
		if (set) 
		{
			currentMesh.material.color = Color.cyan;
		} else 
		{
			currentMesh.material.color = teamColor;
		}
		ShowTargets(set);
	}
	public void SetColor(Color NewColor)
	{
		teamColor = NewColor;
		currentMesh.material.color = teamColor;
	}
	void ShowTargets(bool set)
	{
		foreach (GameObject t in targetPins) 
		{
			if(t!=null)
			{
				t.GetComponent<Renderer>().enabled = set;
			}
		}
	}

//	public void Move(Vector3 target)
//	{
//		StopCoroutine("MoveTo");
//		StartCoroutine("MoveTo",target);
//	}
//	IEnumerator MoveTo(Vector3 target)
//	{
//		easeType = ease.ToString();
//		while (Vector3.Distance(transform.position,target)>.1f) 
//		{
//			iTween.MoveTo(gameObject, iTween.Hash("position", target, "easeType", easeType, "loopType", "none", "speed", moveSpeed));
//			//transform.position = Vector3.MoveTowards (transform.position, target, deltaSpeed);
//			yield return new WaitForSeconds(1f);
//		}
//		StopCoroutine("MoveTo");
//	}

	void OnTriggerEnter(Collider other)
	{
		switch(other.tag)
		{
			case "Ball":
			{
				hasBall = true;
				other.transform.SetParent(transform);
				other.transform.position = transform.TransformPoint(0,0,1);
				ball = other.GetComponent<BallScript>();
				break;
			}
		}
	}
	void OnTriggerExit(Collider other)
	{
		switch(other.tag)
		{
			case "Ball":
			{
				hasBall = false;
				other.transform.SetParent(null);
				ball = null;
				break;
			}
		}
	}
}
