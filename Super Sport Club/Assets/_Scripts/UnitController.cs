using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Hashtable = ExitGames.Client.Photon.Hashtable;


public class UnitController : MonoBehaviour
{
	public int id, actionCount, targetCount, maxActions = 2;
	public int team;
	public CharacterData charData; // contains Name, Id, and stats
	public bool hasTarget, hasBall;
	public bool BActive{get{return this.gameObject.activeSelf;}set{this.gameObject.SetActive(value);}}
	public int MoveDistance{get{return  charData.MoveDist;}}
	public bool CanSprint{get{return (turnsSinceSprint>=2)&&(targetCount<2);}}
	public Cell OccupiedCell
	{
		get
		{
			return Grid_Setup.Instance.GetCellByLocation(Location);
		}
	}
	public Cell LastTargetCell
	{
		get{
			if (lastTargetedCell != null)
				return lastTargetedCell;
			else
				return OccupiedCell;
		}
	}
	public Vector3 Location
	{
		get{return tran.position;}
	}
	public bool IsSprinting{get{return bSprinting;}}
//	public enum Stance
//	{
//		Neutral,
//		Move,
//		Sprint,
//		Defend,
//		Pass,
//		Shoot,
//		Cover_Man,
//		Cover_Ball
//	};
	protected string easeType;
	[SerializeField] Vector3 offset = new Vector3(0,0.2f,0);
	[SerializeField] protected iTween.EaseType ease;
	[SerializeField] LayerMask characterLayer;
	[SerializeField] GameObject destPin;
	[SerializeField] Color teamColor;
	[SerializeField] float rayLength = 1.5f, refactory = 0f;
	private int turnsSinceSprint= 2;
	bool bSprinting;
	BallScript ball;
	GameObject[] targetPins;
	GameObject passTargetPin;//this could be put with targetPins[] and just given a different color at runtime
	Queue<PlayerAction> ActionQueue;
	Cell lastTargetedCell;
	MeshRenderer currentMesh;
	//Animator anim;
	Transform tran;
	UnitController opp;
	//NavMeshAgent navAgent;

	void Awake()
	{
		//navAgent = GetComponent<NavMeshAgent> ();
		//CurrentState = Stance.Neutral;
		tran = transform;
		currentMesh = GetComponentInChildren<MeshRenderer>();
		//anim = GetComponentInChildren<Animator>();
		ActionQueue = new Queue<PlayerAction> (maxActions);
		targetPins = new GameObject[maxActions];
		for (int i = 0; i < maxActions; i++) 
		{
			targetPins[i] = Instantiate(destPin,Vector3.zero,Quaternion.identity) as GameObject;

			targetPins [i].SetActive (false);
		}

		passTargetPin = Instantiate(destPin,Vector3.zero,Quaternion.identity) as GameObject;
		passTargetPin.GetComponent<Renderer> ().material.color = Color.blue;
		passTargetPin.SetActive(false);
	}
	void OnEnable()
	{
		UnityEventManager.StartListening ("NextTurn",UpdateTurn);
	}
	void OnDisable()
	{
		UnityEventManager.StopListening ("NextTurn",UpdateTurn);
	}
	void Update()
	{
		Debug.DrawRay(tran.position,tran.forward);
	}
	void UpdateTurn()
	{
		if (bSprinting) 
		{
			turnsSinceSprint = 0;
		}
		turnsSinceSprint++;
		refactory += 1;
	}
	public void SetPlayerAction(PlayerAction act)
	{
		//if(actionCount<maxActions)
		{
			ActionQueue.Enqueue (act);
			actionCount += 1;
		}
	}
	public void SetMoveTarget(Cell targetCell)
	{
		targetPins [targetCount].SetActive(true);
		targetPins [targetCount].transform.position = targetCell.Location;
		targetCount++;
		lastTargetedCell = targetCell;
	}
	public void SetPassTarget(Cell target)
	{
		passTargetPin.SetActive(true);
		passTargetPin.transform.position = target.Location;
	}
	public void MoveTransform(Vector3 newLoc)
	{
		newLoc += offset;
		tran.position = newLoc;
	}
	public void StartSprinting()
	{
		bSprinting = true;
	}

	public Hashtable GetCharacterAsProp()
	{
		Hashtable ht = new Hashtable();
		ht["Location"] = Location;
		ht["Name"] = charData.name;
		ht["ID"] = id; //We Identify characters by their Team number and 0-teamSize id
		ht["Team"] = team; //^^^^
		ht["Strength"] = charData.Strength;
		ht["Speed"] = charData.Speed;
		ht["Defense"] = charData.Defense;
		return ht;
	}

	public void ClearActions()
	{
		actionCount = 0;
		for(int t= 0;t<targetPins.Length;t++)
		{
			targetPins [t].SetActive (false);
		}
		targetCount = 0;
		bSprinting = false;
		passTargetPin.SetActive(false);//if we make this apart of targetPins[], then the loop above will take care of it.
		lastTargetedCell = null;
		ActionQueue.Clear ();//This "should" be unnecessary as it's not set until Execution starts, and it empties during execution
	}

	public IEnumerator ExecuteActions()
	{
		while(ActionQueue.Count>0)
		{
			PlayerAction act = ActionQueue.Dequeue ();
			if(act!=null)
			{
				switch(act.action)
				{
					case PlayerAction.Actions.Move:
					{
						Vector3 target = act.cTo.Location;
						target += offset;
						RotateTowards(target);
						yield return StartCoroutine (MoveTo (target));

//						easeType = ease.ToString();
//						Vector3 dir, nextCell;
//						while(Vector3.Distance(tran.position,target)>0.1f) 
//						{
//							dir = target - (OccupiedCell.Location + offset);
//							nextCell = (OccupiedCell.Location + offset) + dir.normalized;
//							if(CanMove(act.cTo))
//							{
//								iTween.MoveTo(gameObject, iTween.Hash("position", nextCell, "easeType", easeType, "loopType", "none", "speed", charData.Speed));
//								yield return new WaitForSeconds(0.2f);
//							}else break;
//						}
						break;
					}
					case PlayerAction.Actions.Pass:
					{
						if(hasBall)
						{
							RotateTowards(act.cTo.Location+offset);
							ball.BallisticVelocity(act.cTo.Location, 40);
							LetGoOfBall();
						}
						break;
					}
					case PlayerAction.Actions.Shoot:
					{
						if(hasBall)
						{
							RotateTowards(act.cTo.Location+offset);
							ball.BallisticVelocity(act.cTo.Location, 20);
							LetGoOfBall();
							UnityEventManager.TriggerEvent("ShotFired");
						}
						break;
					}
					case PlayerAction.Actions.Fumble:
					{
						if(hasBall)
						{
							ball.BallisticVelocity(act.cTo.Location, 20);
							LetGoOfBall();
						}
						
						break;
					}
					case PlayerAction.Actions.Juke:
					{
						break;
					}
					case PlayerAction.Actions.Tackle:
					{
						Vector3 target = act.cTo.Location;
						if(Vector3.Distance(Location,target)>2)
						{
							Cell newCTo = Grid_Setup.Instance.GetNearestCellToDestination(OccupiedCell.Location, target);
							RotateTowards(newCTo.Location+offset);
							yield return StartCoroutine (MoveTo (newCTo.Location+offset));
						}
						break;
					}
					case PlayerAction.Actions.Block:
					{
						break;
					}
					case PlayerAction.Actions.Steal:
					{
						break;
					}
				}
			}else break;
		}
		ClearActions();	
		yield return null;
	}
	IEnumerator MoveTo(Vector3 target)
	{
		//Vector3 dir, nextCell;
		easeType = ease.ToString();
		while(Vector3.Distance(tran.position,target)>0.1f) 
		{
			//dir = target - (OccupiedCell.Location + offset);
			//nextCell = (OccupiedCell.Location + offset) + dir.normalized;
			OccupiedCell.UnitOccupier = null;
			//if(CanMove(target))
			{
				iTween.MoveTo(gameObject, iTween.Hash("position", target, "easeType", easeType, "loopType", "none", "speed", charData.Speed/2));
				yield return new WaitForSeconds(0.2f);
			}//else break;
		}
		OccupiedCell.UnitOccupier = this;
		//StopCoroutine("MoveTo");
	}
	void RotateTowards(Vector3 target)// this is really a SetRotation function
	{
		Vector3 dir = target - tran.position;
		if(dir!=Vector3.zero)
		{
			Quaternion rotation = Quaternion.LookRotation(dir);
			tran.rotation= rotation;
		}
		//float f = Vector3.Angle(tran.forward,dir);
		//tran.Rotate(Vector3.up,f);
	}

//	UnitController PlayerInFrontOfMe()
//	{
//		Ray ray = new Ray(tran.position,tran.forward);
//		RaycastHit hit = new RaycastHit();;
//		Physics.Raycast(ray,out hit,rayLength,characterLayer);
//		if(hit.collider!=null&& hit.collider!= this.GetComponent<Collider>() && hit.transform.tag == "Player")
//		{
//			return hit.transform.GetComponent<UnitController>();
//		}
//		return null;
//	}

//	bool CanMove(Cell targetCell) 
//	{
//		bool canMove;
//		opp = PlayerInFrontOfMe();
//		if(opp!=null)
//		{
//			Debug.Log("Player ID: "+opp.id);
//			if(targetCell.Location== opp.Location)
//			{
//				if(targetCell==opp.LastTargetCell)
//				{
//					canMove = false;
//				}else{
//					canMove = true;
//				}
//			}else{
//				float dotFace = Vector3.Dot(tran.forward,opp.transform.forward);
//				if(dotFace<0)//facing each other
//				{
//					Debug.Log("Oh, just kiss already");
//					canMove = false;
//				}else{
//					canMove = true;
//				}
//			}
//		}else canMove = true;
//		return canMove;
//	}

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
		passTargetPin.GetComponent<Renderer>().enabled = set;
		foreach (GameObject t in targetPins) 
		{
			if(t!=null)
			{
				t.GetComponent<Renderer>().enabled = set;
			}
		}
	}
		
	void LetGoOfBall()
	{
		refactory = 0f;
		Debug.Log ("ball has left");
		ball.BPosessed = false;
		ball.unitOwner = null;
		ball.transform.SetParent(null);
		ball = null;
		hasBall = false;
	}

	void TakeBall(BallScript bs)
	{
		ball = bs;
		Debug.Log ("I gots da ball");
		ball.BPosessed = true;
		ball.unitOwner = this;
		ball.transform.SetParent(this.tran);
		ball.transform.position = tran.TransformPoint (0, 0, 1);
		hasBall = true;
		ball.StopMe ();
	}

	void OnTriggerEnter(Collider other)
	{
		switch(other.tag)
		{
			case "Ball":
			{
				BallScript theBall = other.GetComponent<BallScript> ();
				if (refactory>1f && !theBall.BPosessed) 
				{
					TakeBall(theBall);
				}
				break;
			}
		}
	}

	void OnTriggerStay(Collider other)
	{
		switch(other.tag)
		{
			case "Ball":
			{
				if(hasBall)
				other.transform.position = tran.TransformPoint (0, 0, 1);
			}
			break;
		}
	}

	void OnTriggerExit(Collider other)
	{
		switch(other.tag)
		{
			case "Ball":
			{
				//LetGoOfBall ();
				break;
			}
		}
	}
}
