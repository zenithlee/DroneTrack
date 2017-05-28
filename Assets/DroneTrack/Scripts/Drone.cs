using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class Drone : MonoBehaviour {

    public VideoPlayer vp;    
    public float VideoOffsetms = 0;

    void StartVideo()
    {
        if ( vp ) { 
        vp.time = VideoOffsetms;
        vp.Play();
        }
    }
    
	// Use this for initialization
	void Start () {
        if ( vp ) { 
        vp.Play();
        vp.prepareCompleted += Vp_prepareCompleted;
        }
        else
        {
            Debug.LogWarning("DRONE VIDEO NOT CONNECTED");
        }
    }

    private void Vp_prepareCompleted(VideoPlayer source)
    {
        //vp.Pause();
    }

    // Update is called once per frame
    void Update () {
		
	}
}
