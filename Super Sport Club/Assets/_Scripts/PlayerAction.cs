using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PlayerAction
{
	public UnitController iCh,tCh;
	public Cell cFrom, cTo;
	public enum Actions{Move, Block, Tackle, Juke, Pass, Shoot, Cross, Fumble, Steal}
	public Actions action; 

	public PlayerAction()
	{
	}
//	public PlayerAction(Actions act, UnitController iCharacter)//Actions done to a character that don't affect anything else
//	{
//		this.action=act; iCh = iCharacter;
//	}
//	public PlayerAction(Actions act, UnitController iCharacter, UnitController tCharacter)// Actions one character does to another
//	{
//		this.action=act; iCh = iCharacter; tCh = tCharacter;
//	}
//	public PlayerAction(Actions act, UnitController iCharacter, Cell tCell)// Actions done by a character on the field, 
//	{
//		this.action=act; iCh = iCharacter; cTo = tCell;
//	}
	public PlayerAction(Actions act, UnitController iCharacter, Cell tCell, Cell fCell)// Actions done by a character from one part of the field to another
	{
		this.action=act; iCh = iCharacter; cTo = tCell; cFrom = fCell;
	}
	public PlayerAction Copy(PlayerAction A)
	{
		return new PlayerAction (A.action,A.iCh, A.cTo, A.cFrom);
	}

	public Hashtable GetActionProp()
	{
		Hashtable actionProp = new Hashtable ();
		
		actionProp.Add("Act",(int)action);
		actionProp.Add ("iCharacter",(int)iCh.id);
		actionProp.Add ("iCharacterTeam",(int)iCh.team);;
		if(tCh!=null)actionProp.Add ("tCharacter",(int)tCh.id);
		if(cTo!=null)actionProp.Add("tCell",(int)cTo.id);
		if(cFrom!=null)actionProp.Add("fCell",(int)cFrom.id);
		return actionProp;
	}
	public static PlayerAction GetActionFromProps(Hashtable ht)
	{
		Actions act = (Actions)ht["Act"];
		int iChId = (int)ht["iCharacter"];
		int iChTeam = (int)ht["iCharacterTeam"];
		Cell tcell = Grid_Setup.Instance.GetCellByID((int)ht["tCell"]);
		Cell fcell = Grid_Setup.Instance.GetCellByID((int)ht["fCell"]);
		return new PlayerAction(act,MainGame.Instance.GetCharacter(iChTeam,iChId),tcell, fcell);
	}
}
