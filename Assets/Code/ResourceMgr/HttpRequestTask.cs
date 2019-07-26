using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ResourceMoudle
{
    class HttpRequestTask : Task
    {
        public HttpRequestTask(IHttp http, string url, int offset, int length)
        {
            http_ = http;
            url_ = url;
            offset_ = offset;
            length_ = length;
        }

        public override IEnumerator Run()
        {
            status = TaskStatus.Start;
            errorMessage = "";

            IHttpRequest request = http_.Get(url_, offset_, length_);

            yield return request.Send();

            if (request.IsError())
            {
                errorMessage = request.ErrorMessage();
                status = TaskStatus.Failed;
            }
            else
            {
                result = request;
                status = TaskStatus.Finished;
            }
        }

        IHttp http_;
        string url_;
        int offset_;
        int length_;
    }
}