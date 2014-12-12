using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MonoTouch.CoreFoundation;
using MonoTouch.Foundation;

namespace WinStoreUcwaAppEvents
{
    public class OAuthToken
    {
        public string GrantType { get; private set; }
        public string AccessToken { get; private set;}
        public double ExpirationTime { get; private set; }
        public OAuthToken(string grant_type, string access_token, double expirationTime)
        {
            this.GrantType = grant_type;
            this.AccessToken = access_token;
            this.ExpirationTime = expirationTime;
        }
    }
    public sealed class SignInParameter
    {
        internal string UserName { get; private set; }
        internal string Password { get; private set; }
        internal UcwaAppAuthenticationTypes AuthType { get; private set; }
        public SignInParameter(string name, string pass, UcwaAppAuthenticationTypes authType)
        {
            this.UserName = name;
            this.Password = pass;
            this.AuthType = authType;
        }
    }
    public class UcwaAppUtils
    {
        #region helper methods
        public static IEnumerable<KeyValuePair<string, string>> ConvertWebHeaderCollectionToKeyValuePairs(WebHeaderCollection headerCollection)
        {
            List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();
            foreach (var headerName in headerCollection.AllKeys)
            {
                var headerValue = headerCollection[headerName];
                var kvPair = new KeyValuePair<string, string>(headerName, headerValue);
                headers.Add(kvPair);
            }
            return headers.AsEnumerable<KeyValuePair<string, string>>();
        }
        public static string ConvertResponseBodyStreamToString(Stream responseStream)
        {
            string responseBody = null;
            using (StreamReader sr = new StreamReader(responseStream))
                responseBody = sr.ReadToEnd();
            return responseBody;
        }

        public static void ReportError(UcwaAppErrorReportEventHandler errorReporter, Exception e)
        {
            if (errorReporter != null)
                errorReporter(e);
        }
        public static void ReportProgress(UcwaAppProgressReportEventHandler progressReporter, string msg, HttpStatusCode status = HttpStatusCode.OK)
        {
            if (progressReporter != null)
                progressReporter(msg, status);
        }


        public static void DispatchEventToUI(NSAction callback)
        {
			if (callback != null)
				DispatchQueue.MainQueue.DispatchAsync (callback);
        }

        //public static void RaiseEvent(MulticastDelegate mDelegate, object sender, EventArgs e)
        //{
        //    InvokeDelegates(mDelegate, sender, e);
        //}
        //public static void InvokeDelegates(MulticastDelegate mDelegate, object sender, EventArgs e)
        //{
        //    if (mDelegate == null)
        //        return;

        //    Delegate[] delegates = mDelegate.GetInvocationList();
        //    if (delegates == null)
        //        return;

        //    // Invoke delegates within their threads
        //    foreach (Delegate _delegate in delegates)
        //    {
        //        //if (_delegate.Target.GetType().ContainsGenericParameters)
        //        //{
        //        //    Console.WriteLine("Cannot invoke event handler on a generic type.");
        //        //    return;
        //        //}

        //        object[] contextAndArgs = { sender, e };
        //        _delegate.DynamicInvoke(contextAndArgs);

        //        //ISynchronizeInvoke syncInvoke = _delegate.Target as ISynchronizeInvoke;
        //        //{
        //        //    if (syncInvoke != null)
        //        //    {
        //        //        // This case applies to the situation when Delegate.Target is a 
        //        //        // Control with an open window handle.
        //        //        syncInvoke.Invoke(_delegate, contextAndArgs);
        //        //    }
        //        //    else
        //        //    {
        //        //        _delegate.DynamicInvoke(contextAndArgs);
        //        //    }
        //        //}
        //    }
        //}

        #endregion helper methods
    }
}
