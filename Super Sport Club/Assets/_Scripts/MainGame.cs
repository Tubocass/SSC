﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon.LoadBalancing;
using Hashtable = ExitGames.Client.Photon.Hashtable;


public class MainGame : MonoBehaviour 
{
	public static MainGame Instance
	{
		get
		{
			if (!instance)
			{
				instance = FindObjectOfType (typeof (MainGame)) as MainGame;

				if (!instance)
				{
					Debug.LogError ("There needs to be one active MainGame script on a GameObject in your scene.");
				}
				else
				{
					instance.StartGame(); 
				}
			}

			return instance;
		}
	}
	public bool bOnline, bDev;
	public float timeSinceService;
	public byte MaxActions = 5;
	public Team[] Teams;
	public int TeamSize{ get{return teamSize;}}
	public int TurnNumber{get{return turnNumber;}}
	public byte ActionsLeft{get{return (byte)(MaxActions - actionCount[CurrentTeamNum]);}}
	public Team CurrentTeam{get{return Teams[CurrentTeamNum];}}
	public int CurrentTeamNum{
		get
		{
			if(bDev)
			return currentTeam;
			else return teamNum;
		}
		set
		{
			if(bDev)
			{
				currentTeam = value;
				teamNum = value;
			}else teamNum = value;
		}
	}
	public PlayerAction[] CurrentActionSet{get{return characterActions[CurrentTeamNum];}set{characterActions[CurrentTeamNum]=value;}}
	[SerializeField] private int teamSize = 5, currentTeam;
	[SerializeField] string AppId;// set in inspector. this is called when the client loaded and is ready to start
	[SerializeField] float serviceInterval = 1;
	[SerializeField] private CharacterData[] positionData;
	[SerializeField] private GameObject charFab = null;
	[SerializeField] private Color[] TeamColors =  {Color.black, Color.white};
	[SerializeField] Vector3[] devPositions;
	int[] score;
	PlayerAction[][] characterActions;
	bool P1Submitted, P2Submitted, connectInProgress;
	int turnNumber, teamNum;//, teamSize = 5;
	byte[] actionCount = new byte[2];
	private CustomGameClient GameClientInstance;
	private GUIController gui;
	private Grid_Setup board;
	private static MainGame instance;


	public void StartGame()
	{
		this.gui = GetComponent<GUIController>();
		this.board = GetComponent<Grid_Setup>();
		Grid_Setup.Instance = this.board;
		this.GameClientInstance  = new CustomGameClient();
		CustomGameClient.ClientInstance = this.GameClientInstance;
		this.GameClientInstance.AppId = AppId;  // edit this!
		this.GameClientInstance.mainGame = this;
		Application.runInBackground = true;
		CustomTypes.Register();
		characterActions = new PlayerAction[][]{new PlayerAction[MaxActions], new PlayerAction[MaxActions], new PlayerAction[10]};
		//oppCharacers = new FSM_Character[teamSize];
		score = new int[2];
		connectInProgress = GameClientInstance.ConnectToRegionMaster("us"); 
	}

	void OnEnable()
	{
		UnityEventManager.StartListeningInt("ScoreGoal", GoalScored);
	}
	void OnDisable()
	{
		UnityEventManager.StopListeningInt("ScoreGoal", GoalScored);
	}

	void Update()
	{
		timeSinceService += Time.deltaTime;
		if (timeSinceService > serviceInterval)
		{
			this.GameClientInstance.Service();
			timeSinceService = 0;
		}
	}

	public void SetPlayerAction(PlayerAction act)
	{
		//if(actionCount[CurrentTeamNum]<MaxActions&&act.iCh.actionCount<act.iCh.maxActions)
		{
			CurrentActionSet[actionCount[CurrentTeamNum]] = act;
			actionCount[CurrentTeamNum] += 1;
			if(act.action == PlayerAction.Actions.Move)// preview waypoints
			{
				act.iCh.SetMoveTarget(act.cTo);
			}
			if(act.action == PlayerAction.Actions.Pass || act.action == PlayerAction.Actions.Shoot || act.action == PlayerAction.Actions.Cross)
			{
				act.iCh.SetPassTarget(act.cTo);
			}
		}
	}
	public void RemovePlayerActions(UnitController unit)
	{
		//I want to make this recursive
		PlayerAction temp;
		byte count=0;
		for(int i = 0; i<CurrentActionSet.Length;i++)
		{
			if(CurrentActionSet[i]!= null && CurrentActionSet[i].iCh==unit)
			{	
				count++;
				temp = CurrentActionSet[MaxActions-1];
				CurrentActionSet[i] = temp;
				CurrentActionSet[MaxActions-1] = null;
			}
		}
		actionCount[CurrentTeamNum] -= count;
	}

	public void SetOtherTeamActions(Hashtable ht)
	{
		characterActions[(CurrentTeamNum+1)%2] = LoadActionsFromProps(ht);
	}

	PlayerAction[] LoadActionsFromProps(Hashtable ht)
	{
		PlayerAction[] actions = new PlayerAction[ht.Count];
		for(int i = 0;i<ht.Count;i++)
		{
			if(ht[i.ToString()]!=null)
			{
				Hashtable ion = ht[i.ToString()]as Hashtable;
				actions[i] = PlayerAction.GetActionFromProps(ion);
			}
		}
		return actions;
	}

	public void ClearActions()
	{
		for(int b=0; b<CurrentActionSet.Length; b++)
		{
			if(CurrentActionSet[b]!=null)
			{
				CurrentActionSet[b] = null;
			}
		}
		board.TurnOffHiglightedAdjacent ();
		actionCount[CurrentTeamNum] = 0;
		foreach(UnitController c in CurrentTeam.mates)
		{
			c.ClearActions();
		}
	}

	public bool IsShotOnGoal(int tNum, Vector3 spot)
	{
		return Teams[tNum].IsVectorInGoal (spot);
		
	}

	void GoalScored(int team)
	{
		if (team > 1 || team<0) 
		{
			return;
		}
		score [team] += 1;
		gui.UIState = gui.UISP;
		Teams[0].Sleep();
		Teams[1].Sleep();
		board.ResetBoard();
	}

	public int TeamScore(int team)
	{
		if (team > 1 || team<0) 
		{
			return -1;
		}
		return score [team];
	}

	void CreateTeams()
	{
		Teams = new Team[2];
		int d=0;
		for(int t = 0; t<2 ; t++)
		{
			//bool teamOne = t == 0;
			Vector3 goal = t == 0 ? board.TeamOneGoal : board.TeamTwoGoal;
			Teams [t] = new Team (t, TeamColors[t], teamSize, goal, board.GoalSize);
			//Quaternion face = teamOne ? Quaternion.LookRotation(Vector3.right):Quaternion.LookRotation(-Vector3.right) ;
			for(int c = 0; c <teamSize; c++)
			{
				GameObject newGuy = Instantiate(charFab,Vector3.zero + new Vector3((float)t,0.2f,(float)c),Quaternion.identity) as GameObject;
				Teams [t].AddMate(newGuy.GetComponent<UnitController>());
				Teams [t].mates [c].team = t;
				Teams [t].mates [c].charData = positionData [c];
				newGuy.SetActive (false);
				if(bDev)
				{
					SetCharacterPosition(t,c,devPositions[d]);
					d++;
				}
			}
		}
	}

	public UnitController GetCharacter(int Team, int index)
	{
		return Teams [Team].mates [index];
	}

	bool TeamActive(Team team)
	{
		int all=0;
		for(int i = 0; i<teamSize; i++)
		{
			if(team.mates[i].BActive)
			{
				all++;
			}
		}
		return all == teamSize;
	}

	public void SetCharacterPosition(int team,int index, Vector3 location)
	{
		if(team<Teams.Length && index<teamSize)
		{
			Teams[team].mates[index].BActive = true;
			Teams[team].mates[index].MoveTransform(board.GetCellByLocation(location).Location);
			board.GetCellByLocation(location).UnitOccupier = Teams[team].mates[index];
			//have a counter or signal here
		}
	}

	public void LoadCharactersFromProps(Hashtable ht)
	{
		for(int i = 0;i<ht.Count;i++)
		{
			Hashtable hash = ht[i.ToString()]as Hashtable;
			int team = (int)hash["Team"];
			Vector3 loc = (Vector3)hash["Location"];
			this.SetCharacterPosition(team, i, loc);
		}
	}

	public void Connect()
	{
		//connectInProgress = GameClientInstance.ConnectToRegionMaster("us");  // can return false for errors
		//StartCoroutine("NewOnlineGame");
		if (connectInProgress) 
			{
				//GameClientInstance.CreateTurnbasedRoom();
				GameClientInstance.OpJoinRandomRoom (null, 0);
				bOnline = true;

			} else {
				connectInProgress = GameClientInstance.ConnectToRegionMaster("us"); 
				Debug.Log ("I Can't Even");
			}
	}

	public void NewGame()
	{
		board.Generate();
		CreateTeams();
		serviceInterval = 1f;
		if(bDev)
			gui.UIState.ToGameHUD();
		else gui.UIState.ToSetPiece();

	}

	public void SubmitTeam()
	{
		if(TeamActive(CurrentTeam))
		{
			if(bOnline)
			this.GameClientInstance.SubmitTeamEvent(CurrentTeam);

			if(bDev&&!(TeamActive(Teams[0])&&TeamActive(Teams[1])))
			{
				CurrentTeamNum = (CurrentTeamNum+1)%2;
				gui.UIState.ToGameHUD();
				gui.UIState.ToSetPiece();
			}else{
				gui.UIState.ToGameHUD();
				NextTurn();
			}


		}else {
			Debug.Log ("You still have players to place");
		}
	}

	void NextTurn()
	{
		ClearActions();
		actionCount[0] = 0;
		actionCount[1] = 0;
		UnityEventManager.TriggerEvent ("NextTurn");
		turnNumber++;
		if(!bOnline)
		{
			P1Submitted = false;
			P2Submitted = false;
		}
	}

	bool BothPlayersHaveSubmitted()
	{
		return P1Submitted && P2Submitted;
	}

	public void EndTurn()
	{
		if(bOnline)
		this.GameClientInstance.EndTurnEvent(CurrentActionSet);
		else {
			if(CurrentTeamNum==0)
			P1Submitted=true;
			else P2Submitted=true;

			if(BothPlayersHaveSubmitted())
				CalcMoves();
			else{ CurrentTeamNum = (CurrentTeamNum+1)%2; gui.UIState.DeselectCharacter();}
		}
	}


	public void CalcMoves()
	{
	//The idea is to sort each Player's actions to figure out what will actually happen vs plans. 
	//Compiled list of acts is then acted upon by both Players
		Hashtable MoveSet = new Hashtable();
		//List<Cell> targetedCells = new List<Cell> ();
		List<Vector3> moveVectors = new List<Vector3>();
		Debug.Log ("Calculating");
		int count = 0;
		for(int k = 0; k<MaxActions; k++)
		{
			if(characterActions[0][k]!=null)
			{
				if(characterActions[0][k].action == PlayerAction.Actions.Move)
				{
					for(int j = 0;j<characterActions[1].Length; j++)
					{
						if(characterActions[1][j]!=null)
						{
							if(characterActions[1][j].action == PlayerAction.Actions.Move)//both moving
							{
								Vector point;
								Vector p = new Vector(characterActions[0][k].cFrom.Location.x,characterActions[0][k].cFrom.Location.z);
								Vector p2 = new Vector(characterActions[0][k].cTo.Location.x,characterActions[0][k].cTo.Location.z);
								Vector q = new Vector(characterActions[1][j].cFrom.Location.x,characterActions[1][j].cFrom.Location.z);
								Vector q2 = new Vector(characterActions[1][j].cTo.Location.x,characterActions[1][j].cTo.Location.z);
								bool intersect = LineIntersection(p, p2, q, q2, out point);
								Debug.Log(intersect+ ", "+ point.ToString());
								if(characterActions[0][k].cTo == characterActions[1][j].cTo)
								{
									float dist1, dist2;
									dist1 = (characterActions[0][k].cTo.Location-characterActions[0][k].cFrom.Location).sqrMagnitude/characterActions[0][k].iCh.charData.Speed;// Vector3.Distance(characterActions[0][k].cFrom.Location, characterActions[0][k].cTo.Location);
									dist2 = (characterActions[1][j].cTo.Location-characterActions[1][j].cFrom.Location).sqrMagnitude/characterActions[1][j].iCh.charData.Speed;
									if(dist1> dist2)
									{
										Cell newCTo = board.GetNearestCellToDestination(characterActions[0][k].cFrom.Location, characterActions[0][k].cTo.Location);
										characterActions[0][k].cTo = newCTo;
									}
									else if(dist1<dist2)
									{
										Cell newCTo = board.GetNearestCellToDestination(characterActions[1][j].cFrom.Location, characterActions[1][j].cTo.Location);
										characterActions[1][j].cTo = newCTo;
									}else {
										
									}
								}
							}
						}
					}
				}

//				if(characterActions[0][k].action == PlayerAction.Actions.Move)
//				{
//					moveVectors.Add(characterActions[0][k].cTo-characterActions[0][k].cFrom);
//				}
				if(characterActions[0][k].action == PlayerAction.Actions.Tackle)
				{
					Cell targetCell = characterActions[0][k].cTo;
					if(targetCell.bOccupied && targetCell.UnitOccupier.team!= characterActions[0][k].iCh.team)//This will also not be allowed against team mates in the first place
					{
						UnitController unitTarget = targetCell.UnitOccupier;
						if(targetCell.UnitOccupier.LastTargetCell == targetCell)//if the person I'm targeting is "landed" on my target tile
						{
							bool tackleSuccess = (unitTarget.charData.Defense+Random.Range(1,5)>= characterActions[0][k].iCh.charData.Strength+Random.Range(1,3));
							Debug.Log(tackleSuccess.ToString());
							if(tackleSuccess)
							{
								characterActions[2][count] = (new PlayerAction(PlayerAction.Actions.Fumble,unitTarget,characterActions[0][k].iCh.OccupiedCell,unitTarget.OccupiedCell));
								count++;
							}
						}
					}
				}
			}
		}

		count = 0;
		for(int j = 0;j<characterActions.Length; j++)
		{
			for (int i = 0; i<characterActions[j].Length; i++) 
			{
				if(characterActions[j][i]!=null)
				{
					MoveSet[count.ToString()] = characterActions[j][i].GetActionProp();
					count++;
				}
			}
		}
		GameClientInstance.OpRaiseEvent((byte)2, MoveSet, true, null);
		ExecuteMoves(MoveSet);
	}

	public void ExecuteMoves(Hashtable moves)
	{
		PlayerAction[] acts = LoadActionsFromProps (moves);
		List<UnitController> affectedChars = new List<UnitController>();
		for (int h = 0; h < acts.Length; h++) 
		{
			if(acts[h]!=null)
			{
				UnitController unit = acts[h].iCh;
				Debug.Log(acts[h].action.ToString()+", "+acts[h].cTo.Location);
				if(!affectedChars.Contains(unit))
				{
					affectedChars.Add(unit);
				}
				unit.SetPlayerAction(acts[h]);
			}
		}
		foreach(UnitController c in affectedChars)
		{
			c.StartCoroutine("ExecuteActions");
		}

		NextTurn ();
	}

	bool LineIntersection(Vector p, Vector p2, Vector q, Vector q2, out Vector intersection)
	{
		intersection = new Vector();
		var r = p2-p;
		var s = q2-q;
		var rxs = r.Cross(s);
		var qpxr = (q-p).Cross(r);
	// var r = p2 - p;
    // var s = q2 - q;
    // var rxs = r.Cross(s);
    // var qpxr = (q - p).Cross(r);

	// If r x s = 0 and (q - p) x r = 0, then the two lines are collinear.
    if (rxs.IsZero() && qpxr.IsZero())
    {
        // 1. If either  0 <= (q - p) * r <= r * r or 0 <= (p - q) * s <= * s
        // then the two lines are overlapping,
       // if (considerCollinearOverlapAsIntersect)
//            if ((0 <= (q - p)*r && (q - p)*r <= r*r) || (0 <= (p - q)*s && (p - q)*s <= s*s))
//                return true;

        // 2. If neither 0 <= (q - p) * r = r * r nor 0 <= (p - q) * s <= s * s
        // then the two lines are collinear but disjoint.
        // No need to implement this expression, as it follows from the expression above.
        return false;
    }


    // 3. If r x s = 0 and (q - p) x r != 0, then the two lines are parallel and non-intersecting.
	if (rxs.IsZero() && !qpxr.IsZero())
        return false;

    // t = (q - p) x s / (r x s)
	var t = (q-p).Cross(s)/rxs;

    // u = (q - p) x r / (r x s)

	var u = (q-p).Cross(r)/rxs;

    // 4. If r x s != 0 and 0 <= t <= 1 and 0 <= u <= 1
    // the two line segments meet at the point p + t r = q + u s.
	if (!rxs.IsZero() && (0 <= t && t <= 1) && (0 <= u && u <= 1))
    {
        // We can calculate the intersection point using either t or u.
        intersection = p + t*r;

        // An intersection was found.
        return true;
    }

    // 5. Otherwise, the two line segments are not parallel but do not intersect.
    return false;
 
	}
	/// <summary>
    /// Returns the intersection point of the given lines. 
    /// Returns Empty if the lines do not intersect.
    /// Source: http://mathworld.wolfram.com/Line-LineIntersection.html
    /// </summary>
//	public static Vector2 LineIntersection(Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4)
//    {
//        float tolerance = 0.000001f;
//
//        float a = Det2(v1.x - v2.x, v1.y - v2.y, v3.x - v4.x, v3.y - v4.y);
//		if (Mathf.Abs(a) < float.Epsilon) return Vector2.zero; // Lines are parallel
//
//        float d1 = Det2(v1.X, v1.Y, v2.X, v2.Y);
//        float d2 = Det2(v3.X, v3.Y, v4.X, v4.Y);
//        float x = Det2(d1, v1.X - v2.X, d2, v3.X - v4.X) / a;
//        float y = Det2(d1, v1.Y - v2.Y, d2, v3.Y - v4.Y) / a;
//
//        if (x < Math.Min(v1.X, v2.X) - tolerance || x > Math.Max(v1.X, v2.X) + tolerance) return PointF.Empty;
//        if (y < Math.Min(v1.Y, v2.Y) - tolerance || y > Math.Max(v1.Y, v2.Y) + tolerance) return PointF.Empty;
//        if (x < Math.Min(v3.X, v4.X) - tolerance || x > Math.Max(v3.X, v4.X) + tolerance) return PointF.Empty;
//        if (y < Math.Min(v3.Y, v4.Y) - tolerance || y > Math.Max(v3.Y, v4.Y) + tolerance) return PointF.Empty;
//
//        return new PointF(x, y);
//    }
//
//    /// <summary>
//    /// Returns the determinant of the 2x2 matrix defined as
//    /// <list>
//    /// <item>| x1 x2 |</item>
//    /// <item>| y1 y2 |</item>
//    /// </list>
//    /// </summary>
//    public static float Det2(float x1, float x2, float y1, float y2)
//    {
//        return (x1 * y2 - y1 * x2);
//    }
}
public class Vector
{
    public double X;
    public double Y;

    // Constructors.
    public Vector(double x, double y) { X = x; Y = y; }
    public Vector() : this(double.NaN, double.NaN) {}

    public static Vector operator -(Vector v, Vector w)
    {
        return new Vector(v.X - w.X, v.Y - w.Y);
    }

    public static Vector operator +(Vector v, Vector w)
    {
        return new Vector(v.X + w.X, v.Y + w.Y);
    }

    public static double operator *(Vector v, Vector w)
    {
        return v.X * w.X + v.Y * w.Y;
    }

    public static Vector operator *(Vector v, double mult)
    {
        return new Vector(v.X * mult, v.Y * mult);
    }

    public static Vector operator *(double mult, Vector v)
    {
        return new Vector(v.X * mult, v.Y * mult);
    }

     public string ToString()
    {
    	return X+ ", "+ Y;
    }

    public double Cross(Vector v)
    {
        return X * v.Y - Y * v.X;
    }

    public override bool Equals(object obj)
    {
        var v = (Vector)obj;
        return (X - v.X).IsZero() && (Y - v.Y).IsZero();
    }
}
public static class Extensions
{
    private const double Epsilon = 1e-10;

    public static bool IsZero(this double d)
    {
        return Mathf.Abs((float)d) < Epsilon;
    }
}
