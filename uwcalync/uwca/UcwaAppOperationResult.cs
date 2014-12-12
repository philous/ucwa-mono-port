using System;
using System.Net;
using System.Net.Http.Headers;

namespace WinStoreUcwaAppEvents
{
    public class UcwaAppOperationResult
    {
        public UcwaResource Resource { get; private set; }
        public string ResponseBody { get; private set; }
        public HttpResponseHeaders ResponseHeaders { get; private set; }
        public HttpStatusCode StatusCode { get; private set; }
        public Exception Exception { get; private set; }

        public UcwaAppOperationResult(HttpStatusCode status)
        {
            this.StatusCode = status;
        }
        public UcwaAppOperationResult(HttpStatusCode status, HttpResponseHeaders httpHeaders, UcwaResource res)
        {
            this.Resource = res;
            this.ResponseHeaders = httpHeaders;
            this.StatusCode = status;
        }

        public UcwaAppOperationResult(HttpStatusCode status, HttpResponseHeaders httpHeaders, string httpContent, Exception e)
        {
            this.StatusCode = status;
            this.Exception = e;
            this.ResponseHeaders = httpHeaders;
            this.ResponseBody = httpContent;
        }
        public UcwaAppOperationResult(HttpStatusCode status, HttpResponseHeaders httpHeaders)
        {
            this.StatusCode = status;
            this.ResponseHeaders = httpHeaders;
        }
        public UcwaAppOperationResult(HttpStatusCode status, Exception e)
        {
            this.StatusCode = status;
            this.Exception = e;
        }
        
    }
}
