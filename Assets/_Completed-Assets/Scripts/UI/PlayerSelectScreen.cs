using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Complete {  
public class PlayerSelectScreen : MonoBehaviour
{
	public GameObject playerSelectUI;
	public GameObject ControlUI;
	
	
	public void OnePlayer()
	{
		gameObject.GetComponent<GameManager>().m_AiTanks = 1;
		PlayerSelected();
	}

	public void TwoPlayer()
	{
		gameObject.GetComponent<GameManager>().m_AiTanks = 0;
		PlayerSelected();
	}

	public void NoPlayer()
	{
		gameObject.GetComponent<GameManager>().m_AiTanks = 2;
		PlayerSelected();
	}

	void PlayerSelected()
	{
		playerSelectUI.SetActive(false);
		ControlUI.SetActive(false);
		gameObject.GetComponent<GameManager>().enabled = true;
	}
}
}
