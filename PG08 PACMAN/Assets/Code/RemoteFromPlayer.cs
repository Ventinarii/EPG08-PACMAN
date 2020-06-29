using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemoteFromPlayer : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GM = GameObject.Find("Main Camera").GetComponent<GameMaster>(); ;
    }
    GameMaster GM;
    //playerColider
    private void OnTriggerEnter(Collider other)
    {
        GM.PlayerGhostColide(other);
    }
}
