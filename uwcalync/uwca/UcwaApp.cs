using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Json;

namespace WinStoreUcwaAppEvents
{
    public class UcwaApp 
    {

        bool discoverFromInternalDomain = false;
        UcwaAppAuthenticationTypes authenticationType = UcwaAppAuthenticationTypes.Password;
		private string discoverUrl;
        string applicationsUrl;
        string userName, password;
        string appSettingsFormatter =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<input xmlns=\"http://schemas.microsoft.com/rtc/2012/03/ucwa\">" +
            "   <property name=\"culture\">{0}</property>" +
            "   <property name=\"endpointId\">{1}</property>" +
            "   <property name=\"userAgent\">{2}</property>" +
            "</input>";
        private Timer timer;

        public UcwaAppEventNotificationsReceivedEventHandler OnEventNotificationsReceived;   // delegate for parsing the events on the calling (UI) thread
        public UcwaAppEventChannelClosedEventHandler OnEventChannelTerminated;
        public UcwaAppEventsReceivedEventHandler OnEventsReceived;

        public UcwaAppProgressReportEventHandler OnProgressReported; // delegate for reporting progress on the calling UI thread
        public UcwaAppErrorReportEventHandler OnErrorReported; // delegate for reporting errors on the callling UI thread

        public UcwaAppTransport Transport { get; private set; }
        public bool IsSignedIn { get; set; }
        public UcwaResource ApplicationResource { get; private set; }
        public UcwaAppMe Me { get; private set; }
        public UcwaApp(bool discoverFromInternalDomain = false)
        {
            Transport = new UcwaAppTransport();
            this.IsSignedIn = false;
            this.discoverFromInternalDomain = discoverFromInternalDomain;
        }
        public async Task<HttpStatusCode> SignIn(string userName, string password, UcwaAppAuthenticationTypes authType=UcwaAppAuthenticationTypes.Password)
        {
            this.userName = userName;
            this.password = password;
            this.authenticationType = authType;
            try
            {
                var opResult = await DiscoverRootResource(this.discoverFromInternalDomain);
                if (opResult.Resource == null)
                {
                    UcwaAppUtils.ReportProgress(OnProgressReported, "GetRootResource returns null result.", opResult.StatusCode);
                    return opResult.StatusCode;
                }
				//TODO authenticating with token
                //opResult = await GetUserResource(opResult.Resource.GetLinkUri("user"), userName, password, authType);
				opResult = await GetUserResourceByToken(opResult.Resource.GetLinkUri("user"), "Bearer", "cwt=AAEBHAEFAAAAAAAFFQAAAJETUyiWZo0v_Pup1T0MAACBEO5FCgPWI0dWjidZXCapsQOCAo1dgyDabUjQhT0c2wH910SJKviFnat5sAtbTgWloi9Tj6M6goYITHJYorji0QgNEE2tjE-T29Jfmt6q60DTuZo");
                if (opResult.Resource == null)
                {
                    UcwaAppUtils.ReportProgress(OnProgressReported, 
                        userName + " cannot be authenticated, with the " + authType.ToString() + " grant_type.", 
                        opResult.StatusCode);
                    return opResult.StatusCode;
                }
                    
                // Create the UCWA application bound to the specified user
                opResult = await GetApplicationResource(opResult.Resource);

                if (opResult.Resource == null)
                {
                    UcwaAppUtils.ReportProgress(OnProgressReported, "Failed to create the UCWA application resource.", opResult.StatusCode);
                    return opResult.StatusCode;
                }

                this.ApplicationResource = opResult.Resource;
                UcwaAppUtils.ReportProgress(OnProgressReported, "Succeded in creating the application resource: " + this.ApplicationResource.Uri);

                // Setup and start event channel
                var eventsUri = this.ApplicationResource.GetLinkUri("events");
                this.EventChannel = new UcwaAppEventChannel(eventsUri, this.Transport);
                this.EventChannel.OnEventNotificationsReceived += EventChannel_OnEventNotificationsReceived;
                this.EventChannel.OnErrorReported += EventChannel_OnErrorReported;
				this.EventChannel.OnEventChannelClosed += EventChannel_OnEventChannelClosed;
                this.EventChannel.OnProgressReported += EventChannel_OnProgressReported;   
                this.EventChannel.Start();
                UcwaAppUtils.ReportProgress(OnProgressReported, "Event channel started on " + eventsUri);

                // Make me available to receiving incoming alerts
                this.Me = new UcwaAppMe(this);
                var result = await this.Me.PostMakeMeAvailable("4255552222", "Online", new string[] { "Plain", "Html" }, new string[] { "PhoneAudio", "Messaging"});
                if (result.StatusCode != HttpStatusCode.NoContent)
                {
                    UcwaAppUtils.ReportProgress(OnProgressReported, "Failed to post to makeMeAvailable resource.", result.StatusCode);
                    return result.StatusCode;
                }

                // Get application resource again to receive any updates triggered by the POST request to making me available
                opResult = await GetApplicationResource(this.ApplicationResource.Uri);
                if (opResult.Resource == null)
                {
                    UcwaAppUtils.ReportProgress(OnProgressReported, "Failed to get the updated application resource", opResult.StatusCode);
                    return opResult.StatusCode;
                }
                this.ApplicationResource = opResult.Resource;
                await this.Me.Refresh(this.ApplicationResource.GetEmbeddedResourceUri("me"));

                // Set up a timer to post on reportMyActivity every four minutes
                timer = new Timer((e) =>
                    {ReportMyActivity(this.ApplicationResource.GetEmbeddedResource("me").GetLinkUri("reportMyActivity"));}, 
                    null, 
                    new TimeSpan(0, 0, 0), 
                    new TimeSpan(0, 4, 0)
                );
            }
            catch(Exception ex)
            {
                UcwaAppUtils.ReportError(OnErrorReported, ex);
                return HttpStatusCode.BadRequest;
            }
            return HttpStatusCode.OK;
        }
        async void ReportMyActivity(string uri)
        {
            //await this.Transport.PostResourceAsync(uri, string.Empty);
            HttpContent content = null;
            await this.Transport.PostResourceAsync(uri, content);
        }
        void EventChannel_OnEventNotificationsReceived(UcwaEventsData events)
        {
            UcwaEventsData eventsData = events as UcwaEventsData;
            if (eventsData == null)
                return;
            if (this.OnEventNotificationsReceived != null)
                UcwaAppUtils.DispatchEventToUI(() => this.OnEventNotificationsReceived (eventsData));

            foreach (var sender in eventsData.SenderNames)
            {
                if (OnEventsReceived != null)
                    UcwaAppUtils.DispatchEventToUI(() => OnEventsReceived (sender, eventsData.GetEventsBySender (sender)));
            }
        }
        bool restartEventChannel = false;
        void EventChannel_OnErrorReported(Exception e)
        {
            // connection interrupted.
            // report error and restart the event channel
            if (OnErrorReported != null)
                UcwaAppUtils.DispatchEventToUI(() => OnErrorReported (e));


            // await CleanUp();
            this.restartEventChannel = true;
            //this.EventChannel.Start();
            //this.EventChannel.Restart();
            
        }

        void EventChannel_OnEventChannelClosed(TaskStatus status)
        {
            if (OnEventChannelTerminated != null)
                UcwaAppUtils.DispatchEventToUI(() => OnEventChannelTerminated (status));
            if (this.restartEventChannel)
                this.EventChannel.Start();
        }

        void EventChannel_OnProgressReported(string msg, HttpStatusCode status)
        {
			if (OnProgressReported != null)
                UcwaAppUtils.DispatchEventToUI(() => OnProgressReported (msg, status));
        }

        public async Task<HttpStatusCode> Refresh()
        {
            return await this.SignIn(this.userName, this.password);
        }
        public bool InternalDomain;
        #region Auto-discovery routines
        private async Task<UcwaAppOperationResult> DiscoverRootResource(bool discoverFromInternalDomain = false)
        {
            this.discoverFromInternalDomain = discoverFromInternalDomain;
            //string domain = this.userName.Contains("@") ? this.userName.Split('@')[1] : null;

            string domain = "gotuc.net";
            discoverUrl = "https://lyncdiscoverinternal." + domain;
            if (!this.discoverFromInternalDomain)
                discoverUrl = "https://lyncdiscover." + domain;

            var opResult = await GetRootResource(discoverUrl, maxDiscoverTrials);
            if (opResult.Resource == null)
            {
                if (this.discoverFromInternalDomain)
                {
                    discoverUrl= "https://lyncdiscover." + domain;
                    this.InternalDomain = false;
                    opResult = await GetRootResource(discoverUrl, maxDiscoverTrials);
                }
                if (opResult.Resource == null)
                    return opResult;
            }
            if (discoverUrl.ToLower().Contains("lyncdiscoverinternal"))
                this.InternalDomain = true;
            else
                this.InternalDomain = false;

            string redirectUrl = opResult.Resource.GetLinkUri("redirect");
            if (!string.IsNullOrEmpty(redirectUrl) && RedirectUrlSecurityCheckPassed(redirectUrl))
            {
                opResult = await GetRedirectResource(redirectUrl);
            }
            var arr = opResult.Resource.Uri.Split('/');
            this.Transport.SetBaseAddress(new Uri(arr[0] + "//" + arr[2]));
            return opResult;
        }

        int maxDiscoverTrials = 3;
        private async Task<UcwaAppOperationResult> GetRootResource(string url, int maxTrials=3)
        {
            int trials = 0;
            while (trials < maxTrials)
            {
                trials++;
                var result = await Transport.GetResourceAsync(url);
                if (result.StatusCode == HttpStatusCode.OK)
                    return result;
            }
            return new UcwaAppOperationResult(HttpStatusCode.NotFound, new Exception("Failed to get root resource on " + url)) ;
        }

        private async Task<UcwaAppOperationResult> GetRedirectResource(string redirectUrl, bool checkRedirectUrl = true)
        {
            if (checkRedirectUrl && !RedirectUrlSecurityCheckPassed(redirectUrl))
            {
                return new UcwaAppOperationResult(HttpStatusCode.Redirect, 
                    new Exception("Failed to pass secury check on redirect of " + redirectUrl));
            }
            var result = await Transport.GetResourceAsync(redirectUrl);
            if (result.StatusCode == HttpStatusCode.OK)
                return result;
            return new UcwaAppOperationResult(result.StatusCode, result.ResponseHeaders, result.ResponseBody, new Exception("Failed to execute GetRedirectResource"));
        }

        bool RedirectUrlSecurityCheckPassed(string redirectUrl)
        {
            // See the Security check section in http://ucwa.lync.com/documentation/GettingStarted-RootURL
            // do security verification of the supplied redirectUrl, if (not valid) return false;
            bool isHttps;
            var domain = ParseDomainFromUrl(redirectUrl, out isHttps);
            if (!isHttps) return true;

            if (IsGlobalTrustedDomain(domain)) return true;

            // Prompt user and manage approved list of host names for specific sign-in address domain
            // to do ...

            // Check if host name in the redirect URL in he approved list for specific sign-in address?
            // to do ... if so, return true;

            
            return false;
        }
        private string ParseDomainFromUrl(string url, out bool isHttps)
        {
            string pattern = @"(http[s]?)://[\w|\d|-]+.([\w|\d|-]+.[\w|\d|-]+)/";
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
            var match = regex.Match(url);
            isHttps = match.Groups[1].Value.ToLower() == "s";
            var domain = match.Groups[2].Value;
            return domain;
        }
        bool IsGlobalTrustedDomain(string domain)
        {
            // to do
            return true;
        }
        #endregion Auto-discovery routines
        string ParseOAuthServiceUri(HttpResponseHeaders responseHeaders)
        {
            if (!responseHeaders.Contains("WWW-Authenticate"))
                return null;
            foreach(var parameters in responseHeaders.Where(h=>h.Key=="WWW-Authenticate").Select(i=>i.Value))
            {
                foreach (var p in parameters)
                    if (p.Contains("MsRtcOAuth href"))
                    {
                        string uri = p.Split(',').Where(s=>s.Contains("MsRtcOAuth")).First().Split('=')[1].Replace("\"", "").Trim();
                        return uri;
                    }                      
            }
            return null;
        }

        private async Task<UcwaAppOperationResult> GetUserResourceByToken(string userResUri, string grantType, string token)
        {
            this.IsSignedIn = false;
            Transport.SetAuthorization(new AuthenticationHeaderValue(grantType, token));

            // Second GET userHref, supplying the required compact-web-ticket (cwt) in an Authorization header
            var result = await Transport.GetResourceAsync(userResUri);
            if (result.StatusCode != HttpStatusCode.OK)
                return new UcwaAppOperationResult(
                    result.StatusCode, result.ResponseHeaders, result.ResponseBody,
                    new Exception("GetRequest on " + userResUri + " with oAuth token of " + token));

            this.IsSignedIn = true;
            return result;
        }

        private async Task<UcwaAppOperationResult> GetUserResource(string userResUri, string userName, string password, UcwaAppAuthenticationTypes authType=UcwaAppAuthenticationTypes.Password)
        {
            this.IsSignedIn = false;
            
             //First GET user resource to retrieve oAuthToken href. 
             //Expect 401 Unauthorized response as an HTML payload

            var result = await Transport.GetResourceAsync(userResUri);
            if (result.StatusCode != HttpStatusCode.Unauthorized && result.StatusCode != HttpStatusCode.OK)
            {
                return new UcwaAppOperationResult(result.StatusCode, new Exception("Failed to GetRequest on " + userResUri));
            }
            if (result.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Get OAuth resource for a Web ticket
                if (result.ResponseHeaders.Contains("WWW-Authenticate"))
                {
                    var authServiceUri = ParseOAuthServiceUri(result.ResponseHeaders);
                    var requestFormData = CreateAuthRequestPayload(userName, password, authType);

                    // Note: the following PostRequest returns a json payload in the responseData, containing the access token, 
                    // as charset ('utf-8')?
                    result = await Transport.PostResourceAsync(authServiceUri, requestFormData);
                    var oAuth20Token = GetOAuthToken(result.ResponseBody);
                    SetTotRefreshOAuthTokenOnExpiration(oAuth20Token.ExpirationTime); 
                    if (oAuth20Token != null)
                    {
                        Transport.SetAuthorization(new AuthenticationHeaderValue(oAuth20Token.GrantType, oAuth20Token.AccessToken));
                        // Second GET userHref, supplying the required compact-web-ticket (cwt) in an Authorization header
                        result = await Transport.GetResourceAsync(userResUri);
                        if (result.StatusCode != HttpStatusCode.OK)
                            return new UcwaAppOperationResult(
                                result.StatusCode, result.ResponseHeaders, result.ResponseBody,
                                new Exception("GetRequest on " + userResUri + " with oAuth token of " + oAuth20Token));

                    }
                    else
                        return new UcwaAppOperationResult(result.StatusCode, result.ResponseHeaders, result.ResponseBody, new Exception("Invalid access token"));

                }
            }

            this.IsSignedIn = true;
            return result;
        }
        private OAuthToken GetOAuthToken(string responseData)
        {
//TODO for now using gotuc.net, where auth parsing is not needed
//            OAuthToken oAuth20Token = null;
//			JsonObject json = JsonObject.Parse(responseData);
//            if (json != null)
//                if (json.ContainsKey("access_token") && json.ContainsKey("token_type"))
//                {
//					JsonValue at;
//					json.TryGetValue ("access_token", out at);
//
//					JsonValue tt;
//					json.TryGetValue("token_type", out tt);
//
//					JsonValue et;
//					json.TryGetValue("expires_in", out et);
//                    double etNumber = 0;
//                    if (et != null)
//					etNumber = double.Parse(et.ToString());
//
//                    if (at == null || tt == null)
//                        return null;
//					oAuth20Token = new OAuthToken(tt.ToString(), at.ToString(), etNumber);
//                    return oAuth20Token;
//                }
//            return oAuth20Token;
			return new OAuthToken (string.Empty, string.Empty, 0);
        }
        IEnumerable<KeyValuePair<string, string>> CreateAuthRequestPayload(string userName, string password, UcwaAppAuthenticationTypes authType)
        {
            KeyValuePair<string, string>[] formData= new KeyValuePair<string,string>[] {};
            var formDataList = formData.ToList();
            switch (authType)
            {
                case UcwaAppAuthenticationTypes.Windows:
                    formDataList.Add(new KeyValuePair<string,string>("grant_type", "urn:microsoft.rtc:windows"));
                    formDataList.Add(new KeyValuePair<string,string>("username", userName));
                    break;
                case UcwaAppAuthenticationTypes.Annonymous:
                    formDataList.Add(new KeyValuePair<string,string>("grant_type", "urn:microsoft.rtc:anonmeeting"));
                    formDataList.Add(new KeyValuePair<string,string>("password", password));
                    formDataList.Add(new KeyValuePair<string,string>("msrtc_conferenceuri", userName));
                    break;
                case UcwaAppAuthenticationTypes.Passive:
                    formDataList.Add(new KeyValuePair<string,string>("grant_type", "urn:microsoft.rtc:passive"));
                    break;
                default:  // password
                    formDataList.Add(new KeyValuePair<string,string>("grant_type", "password"));
                    formDataList.Add(new KeyValuePair<string,string>("password", password));
                    formDataList.Add(new KeyValuePair<string,string>("username", userName));
                    break;            
             };

            return formDataList.AsEnumerable();
        }
        string GetAuthenticationRequestBody(string userName, string password, UcwaAppAuthenticationTypes authType)
        {
            string requestBody = null;
            switch (authType)
            {
                case UcwaAppAuthenticationTypes.Windows:
                    requestBody = "grant_type=urn:microsoft.rtc:windows&username=" + userName;
                    break;
                case UcwaAppAuthenticationTypes.Annonymous:
                    requestBody = "grant_type=urn:microsoft.rtc:anonmeeting&password=" + password + "&msrtc_conferenceuri=" + userName;
                    break;
                case UcwaAppAuthenticationTypes.Passive:
                    requestBody = "grant_type=urn:Microsoft.rtc:passive";
                    break;
                default:  // password
                    requestBody = "grant_type=password&username=" + userName + "&password=" + password;
                    break;
            }

            return requestBody;
        }
        void SetTotRefreshOAuthTokenOnExpiration(double expiresInSeconds)
        {
            int intSeconds = (int)expiresInSeconds;
            int hours = intSeconds/3600;
            int minutes = (intSeconds-3600*hours)/60;
            int seconds = (intSeconds -3600*hours-60*minutes);

//TODO not relevant atm
//            var asyncAction = Windows.System.Threading.ThreadPool.RunAsync(
//                (work) =>
//                {
//                    var timer = Windows.System.Threading.ThreadPoolTimer.CreateTimer(
//                        (handler) => { 
//                            // TODO: submit a token-refresh request
//                        },
//                        new TimeSpan(hours, minutes, seconds));
//                },
//                Windows.System.Threading.WorkItemPriority.Normal
//            );
        }

        /// <summary>
        /// Get an application resource bound to the user's local endpoint
        /// </summary>
        /// <param name="resUser">The authenticated user resource</param>
        /// <param name="userAgent">The name of this application</param>
        /// <param name="culture">The locale of this application</param>
        /// <returns>The application resoure as part of UcwaAppOperationResult</returns>
        async Task<UcwaAppOperationResult> GetApplicationResource(UcwaResource resUser, 
            string userAgent="ContosoApp/1.0 (WinStore)", string culture="en-us")
        {
            applicationsUrl = resUser.GetLinkUri("applications");
            var endpointId = Guid.NewGuid().ToString();
            string appSettings = string.Format(appSettingsFormatter, culture, endpointId, userAgent);
            var result = await Transport.PostResourceAsync(applicationsUrl, appSettings);
            if (result.StatusCode != HttpStatusCode.Created)
                return new UcwaAppOperationResult(result.StatusCode, result.ResponseHeaders, result.ResponseBody, 
                    new Exception( "Failed to PostRequest on " + applicationsUrl));
            return result;
        }

        /// <summary>
        /// An overloaded member to get updated application resource, given the application uri.
        /// </summary>
        /// <param name="appUri">previously returned application uri</param>
        /// <returns>The application resource as part of the UcwaAppOperationResult</returns>
        public async Task<UcwaAppOperationResult> GetApplicationResource(string appUri)
        {
            return await Transport.GetResourceAsync(appUri);    
        }
        public UcwaAppEventChannel EventChannel { get; private set; }
        public string EventChannelUri { get { return this.ApplicationResource.GetLinkUri("events"); } }
    }
}
