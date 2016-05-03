using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIGameHUD : IUIState 
{
	static GameObject panel, meter;
	public UnitController CurrentSelectedChar
	{
		get{return MainGameInstance.GetCharacter(MainGameInstance.CurrentTeamNum,currentID);}
	}
	MainGame MainGameInstance;
	GUIController gui;
	float offsetX = -50,offsetY =0;
	bool isCharacterSelected;
	PanelController pc;
	private Vector3 idealPassDir, simpleDir, offsetDir;
	private Grid_Setup board;
	private int currentID = -1;
	private bool isPassing, isMoving, isShooting, isTackling;
	private enum CharacterSelectionState{None, Passing, Shooting, Moving, Tackling}
	private CharacterSelectionState selectionState;
	//LayerMask mask = (1<<LayerMask.NameToLayer("Ground") | 1<<LayerMask.NameToLayer("Characters"));

	public UIGameHUD(GUIController GUI, MainGame mg)
	{
		this.gui = GUI;
		MainGameInstance = mg;
		board = Grid_Setup.Instance;
		panel = GameObject.Instantiate(gui.panelFab.gameObject, Vector3.zero, Quaternion.identity)as GameObject;
		panel.transform.SetParent(gui.UIcan.transform,false);
		panel.transform.SetAsLastSibling(); 
		pc = panel.GetComponent<PanelController> ();
		pc.HidePanel();
	}
	public void EnterState ()
	{
		UnityEventManager.StartListening("ShotFired", ShotOnGoal);
		gui.EnableHUD (true);
		gui.EnableCharacterSelection(false);
	}

	public void Update ()
	{
		if (Input.GetKeyDown (KeyCode.Escape)) 
		{
			ToMainMenu ();
		}
		if(MainGameInstance.bDev)
		{
			if (Input.GetKeyDown (KeyCode.Space)) 
			{
				ToShotState ();
			}
		}
	}
	public void ExitState()
	{
		UnityEventManager.StopListening("ShotFired",ShotOnGoal);
		gui.EnableHUD (false);
		Debug.Log ("Leaving game State");
		DeselectCharacter ();
	}
	public void ToMainMenu ()
	{
		gui.UIState = gui.UIMM;
	}
	public void ToSetPiece ()
	{
		gui.UIState = gui.UISP;
	}
	void ShotOnGoal()
	{
		iTween.Pause ();
		if(MainGame.Instance.IsShotOnGoal(MainGameInstance.CurrentTeamNum, BallScript.TargetLocation))
		{
			ToShotState();
		}
	}
	public void ToShotState ()
	{
		gui.UIState = gui.UISOG;
	}

	public void ToGameHUD ()
	{
		Debug.Log ("Already in Game HUD State.");
	
	}
	public void EndTurnButton()
	{
		//DeselectCharacter();

	}
	public void ClickOnPlayer(int index)
	{
		isCharacterSelected = true;
		if(index!= currentID)
		{
			DeselectCharacter ();
			currentID = index;
			CurrentSelectedChar.Highlight(true);
		}
		CreateButtonPanel (CurrentSelectedChar.Location);
	}

	public void ClickOnField(Vector3 hit)
	{
		if(isCharacterSelected)
		{
			Cell cell = board.GetCellByLocation (hit);
			if (board.IsInsideHighlighted (cell)) 
			{
				switch(selectionState)
				{
				case CharacterSelectionState.None:
				break;
				case CharacterSelectionState.Moving:
					MainGameInstance.SetPlayerAction(new PlayerAction(PlayerAction.Actions.Move, CurrentSelectedChar, cell, CurrentSelectedChar.LastTargetCell));
					if ((CurrentSelectedChar.targetCount == 1 && CurrentSelectedChar.IsSprinting))  
					{
						board.HighlightAdjacent (x=> !x.bOccupied && x !=null && x.type!=Cell.CellType.OutOfBounds, cell.Location, CurrentSelectedChar.MoveDistance);

					}
					else {
						board.TurnOffHiglightedAdjacent();
						selectionState = CharacterSelectionState.None;
					}
				break;
				case CharacterSelectionState.Passing:
					CreatePlayerMeter (cell.Location, PlayerAction.Actions.Pass);
				break;
				case CharacterSelectionState.Shooting:
					CreatePlayerMeter (cell.Location, PlayerAction.Actions.Shoot);
				break;
				case CharacterSelectionState.Tackling:
					ActionClick(cell, PlayerAction.Actions.Tackle);
				break;
				}
			}
		}
	}
	public void DeselectCharacter()
	{
		if(currentID!=-1)
		{
			selectionState = CharacterSelectionState.None;
			pc.HidePanel();//GameObject.Destroy(panel.gameObject);

			if (meter!=null) 
			{
				GameObject.Destroy(meter.gameObject);
			}
			CurrentSelectedChar.Highlight(false);
			board.TurnOffHiglightedAdjacent();
			currentID = -1;
		}
	}

	void ActionClick(Cell tCell, PlayerAction.Actions act)
	{
		//if (CurrentSelectedChar.maxActions -CurrentSelectedChar.actionCount > 0) 
		{
			MainGameInstance.SetPlayerAction(new PlayerAction (act, CurrentSelectedChar,tCell, CurrentSelectedChar.LastTargetCell));
			board.TurnOffHiglightedAdjacent();
			selectionState = CharacterSelectionState.None;
		}
	}

	void CreatePlayerMeter(Vector3 location, PlayerAction.Actions act)
	{
		Debug.Log(location);
		Vector3 CharacterPosition = CurrentSelectedChar.LastTargetCell.Location;
		idealPassDir = location - CharacterPosition;
		idealPassDir.y = 0;

		float ang = Vector3.Angle(Vector3.forward,idealPassDir.normalized);
		Debug.Log(ang);
		if(ang<45)
		{
			simpleDir = Vector3.forward;
			offsetDir = Vector3.down;
		}
		if(ang>=45&&ang<=135)
		{
			if(idealPassDir.x<0)
			{
				simpleDir = Vector3.left;
			}else simpleDir = Vector3.right;
			offsetDir = -simpleDir;
		}
		if(ang>135)
		{
			simpleDir = Vector3.back;
			offsetDir = Vector3.up;
		}
		if (meter!=null)
		{
			GameObject.Destroy(meter.gameObject);
			pc.HidePanel();
		}
		Vector3 loc = CharacterPosition+Vector3.up*0.2f;
		meter = GameObject.Instantiate(gui.meterFab.gameObject, loc, Quaternion.LookRotation(simpleDir))as GameObject;
		meter.GetComponent<Gauge>().SetIdeal(idealPassDir);

		loc= Camera.main.WorldToScreenPoint(CharacterPosition) + offsetDir*50; //re-using a variable
		pc.ShowPanel(loc);

		pc.AddButton("Kick", false).onClick.AddListener (() => 
		{ 
			Vector3 kick = meter.GetComponent<Gauge>().StopBounce();
			Vector3 cellPos = CharacterPosition + kick.normalized * idealPassDir.magnitude;
			ActionClick(board.GetCellByLocation(cellPos),act);			
			GameObject.Destroy(meter.gameObject);
			pc.HidePanel();//GameObject.Destroy(panel.gameObject);
		});
	}

	void CreateButtonPanel(Vector3 cellLocation)
	{
		Vector3 loc = Camera.main.WorldToScreenPoint(cellLocation);
		loc += new Vector3(offsetX,offsetY,0f);
		pc.ShowPanel(loc);
		Vector3 CharacterPosition = CurrentSelectedChar.LastTargetCell.Location;

		if(CurrentSelectedChar.actionCount > 0|| CurrentSelectedChar.targetCount > 0 || CurrentSelectedChar.IsSprinting) 
		{
			pc.AddButton("Clear", false).onClick.AddListener (() => 
			{ 
				CurrentSelectedChar.ClearActions();
				MainGameInstance.RemovePlayerActions(CurrentSelectedChar);//all actions from this character are erased from main list
				board.TurnOffHiglightedAdjacent();
				pc.HidePanel();
			});
		}
		if (MainGameInstance.ActionsLeft > 0) 
		{
			if ((CurrentSelectedChar.maxActions - CurrentSelectedChar.actionCount > 0))
			{
				if(CurrentSelectedChar.CanSprint  && !CurrentSelectedChar.IsSprinting)
				{
					pc.AddButton ("Sprint", false).onClick.AddListener (() => 
						{ 
							selectionState = CharacterSelectionState.Moving;
							board.HighlightAdjacent (x=> x !=null  && !x.bOccupied && x.type!=Cell.CellType.OutOfBounds,CharacterPosition, CurrentSelectedChar.MoveDistance);
							CurrentSelectedChar.StartSprinting();
							pc.HidePanel();
						});
				}
				if (CurrentSelectedChar.targetCount == 0 || (CurrentSelectedChar.targetCount == 1 && CurrentSelectedChar.IsSprinting)) 
				{
					pc.AddButton ("Move", false).onClick.AddListener (() => 
					{ 
						selectionState = CharacterSelectionState.Moving;
						board.HighlightAdjacent (x=> x !=null  && !x.bOccupied && x.type!=Cell.CellType.OutOfBounds, CharacterPosition, CurrentSelectedChar.MoveDistance);
						pc.HidePanel();//GameObject.Destroy (panel.gameObject);
					});
				}
				if (CurrentSelectedChar.targetCount == 0  && !CurrentSelectedChar.IsSprinting) 
				{
					pc.AddButton ("Tackle", false).onClick.AddListener (() => 
					{ 
						selectionState = CharacterSelectionState.Tackling;
						board.HighlightAdjacent (x=> x !=null  && x.bOccupied && x.UnitOccupier.team!=CurrentSelectedChar.team && x.type!=Cell.CellType.OutOfBounds, CharacterPosition, CurrentSelectedChar.MoveDistance);
						pc.HidePanel();
					});
				}

				if (CurrentSelectedChar.hasBall || CurrentSelectedChar.LastTargetCell.HasBall) 
				{
					int shotDistanceSqrd = (int)CurrentSelectedChar.charData.Strength*(int)CurrentSelectedChar.charData.Strength;

					pc.AddButton("Pass", false).onClick.AddListener (() => 
					{ 
						selectionState = CharacterSelectionState.Passing;
						board.HighlightAdjacent (x=> x !=null && x.type!=Cell.CellType.OutOfBounds&& (x.Location-CharacterPosition).sqrMagnitude<shotDistanceSqrd, CharacterPosition, (int)CurrentSelectedChar.charData.Strength);
						pc.HidePanel();
					});	
					pc.AddButton("Shoot", false).onClick.AddListener (() => 
					{ 
						selectionState = CharacterSelectionState.Shooting;
						board.HighlightAdjacent (x=> x !=null && x.type!=Cell.CellType.OutOfBounds && (x.Location-CharacterPosition).sqrMagnitude<shotDistanceSqrd, CharacterPosition, (int)CurrentSelectedChar.charData.Strength);
						pc.HidePanel();
					});	
				}
			}
		}else pc.HidePanel();
	}
}
