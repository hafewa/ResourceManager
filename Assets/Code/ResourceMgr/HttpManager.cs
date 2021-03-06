﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ResourceMoudle
{
    public class HttpRequestContext : IHttpRequest
    {
        public UnityWebRequest mWebRequest;
        public bool mRange;

        public HttpRequestContext(string url)
        {
            mRange = false;
            mWebRequest = UnityWebRequest.Get(url);
        }

        public HttpRequestContext(string url, int offset, int length)
        {
            mWebRequest = UnityWebRequest.Get(url);
            mWebRequest.SetRequestHeader("Range", "bytes=" + offset + "-" + (offset + length - 1));
            mRange = true;
        }

        public byte[] Data()
        {
            return mWebRequest.downloadHandler.data;
        }

        public string ErrorMessage()
        {
            return mWebRequest.isNetworkError ? mWebRequest.error : mWebRequest.downloadHandler.text;
        }

        public bool IsError()
        {
            return mWebRequest.isNetworkError;
        }

        public IEnumerator Send()
        {
            yield return mWebRequest.SendWebRequest();
        }

        public string Text()
        {
            return mWebRequest.downloadHandler.text;
        }
    }

    public class HttpManager : IHttp
    {
        public IHttpRequest Get(string url)
        {
            return new HttpRequestContext(url);
        }

        public IHttpRequest Get(string url, int offset, int length)
        {
            return new HttpRequestContext(url, offset, length);
        }
    }
}