using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class test : MonoBehaviour {

    public Camera camera;
    public GameObject Target;
    public float distance;
    public Texture2D myTexture2D;
    public RenderTexture targetRenderTexture;

    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {

        Vector3 pos = camera.transform.position + camera.transform.forward * distance;
        Target.transform.position = pos;
        Target.transform.forward = (camera.transform.position - pos).normalized;
        camera.targetTexture = targetRenderTexture;
        if (Input.GetMouseButtonDown(1))
        {

            SaveRenderTextureToPNG(targetRenderTexture,"1","1");
        }
    }

 
    
    //将RenderTexture保存成一张png图片  
    public bool SaveRenderTextureToPNG(RenderTexture rt, string contents, string pngName)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D png = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        byte[] bytes = png.EncodeToPNG();
        if (!Directory.Exists(contents))
            Directory.CreateDirectory(contents);
        FileStream file = File.Open(contents + "/" + pngName + ".png", FileMode.Create);
        BinaryWriter writer = new BinaryWriter(file);
        writer.Write(bytes);
        file.Close();
        Texture2D.DestroyImmediate(png);
        png = null;
        RenderTexture.active = prev;
        return true;

    }
}
