using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoPos : MonoBehaviour {

    public float fov;
    float last;
    public Vector2 textureSize;
    Vector3 pos = Vector3.zero;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        if (fov != last)
        {
            pos.z = textureSize.y / 2 / Mathf.Tan(Mathf.Deg2Rad * (fov / 2));
           transform.localPosition =pos;
        }
	}
}
