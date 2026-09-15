using System.Collections;
using UnityEngine;

public class DoorManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    IEnumerator OpenDoor()
    {
        yield return new WaitForSeconds(0.5f);
    }
}
