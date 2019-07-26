using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace ResourceMoudle
{
    public interface IHttpRequest
    {
        bool IsError();
        string ErrorMessage();

        IEnumerator Send();

        string Text();
        byte[] Data();
    }

    public interface IHttp
    {
        IHttpRequest Get(string url);
        IHttpRequest Get(string url, int offset, int length);
    }

 
  
}