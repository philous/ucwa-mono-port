using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace WinStoreUcwaAppEvents
{
    /// <summary>
    /// https://ucwa.lync.com/documentation/programmingconcepts-events
    ///
    /// 1. Requesting events
    ///     a. first P-GET: url of the "events" resource returned from an "application" resource, "ack=1"
    ///     b. subsequent P-GETs: url pointed to by the "next" link from the event channel resposne, "ack=N" (>1)
    ///     c. aggregation of events: pending query paraeters "&medium=T1&low=T2&timeout=T3". 
    /// 2. Timeouts
    ///     a. A timeout returns an empty response, with just a "next" link.
    ///     b. When a request jumps queue, a "409/PGetReplace" will be returned to the earlier request.
    ///     c. A request with a higher "priority" value in the query parameters overrides the pending one of lower priority.
    /// 3. Resume
    ///     a. client stops sending P-Get request for a long time
    ///     b. server cleans up its states to conserve resource, ending active presence subscripiton and closing all conversations
    ///     c. client reactivates and
    ///     d. server returns a "resume" link in place of the "next" link with transient state info.
    /// 4. Resync
    ///     a. client sends an out-of-order PGet request, e.g., using a staled "next" link
    ///     b. server returns a "resync" link, pointing to the last unacknowledged event packet.
    ///    
    ///  https://ucwa.lync.com/documentation/GettingStarted-Events
    ///  1. Event format:
    ///     <?xml version="1.0" encoding="utf-8"?> 
    ///  <events href="/ucwa/oauth/v1/applications/102/events?ack=1" xmlns="http://schemas.microsoft.com/rtc/2012/03/ucwa"> 
    ///    <link rel="next" href="/ucwa/oauth/v1/applications/102/events?ack=2" /> 
    ///    <sender rel="sending resource name" href="sender's link"> 
    ///      <updated rel="updated resource name" href="updated resource link"> 
    ///        <resource rel="resourceName" href="..."> 
    ///          ...
    ///          <link rel="..." href="..." /> 
    ///          <property name="56de7bbf-1081-43e6-bbf2-1cabf3224c83">please pass this in a PUT request</property> 
    ///          <propertyList name="supportedModalities"> 
    ///            <item>Messaging</item> 
    ///          </propertyList> 
    ///        </resource> 
    ///      </updated> 
    ///    </sender> 
    ///    <sender rel="me" href="/ucwa/oauth/v1/applications/102/me"> 
    ///      <updated rel="me" href="/ucwa/oauth/v1/applications/102/me" /> 
    ///      <added rel="presence" href="/ucwa/oauth/v1/applications/102/me/presence" /> 
    ///      <added rel="note" href="/ucwa/oauth/v1/applications/102/me/note" /> 
    ///      <added rel="location" href="/ucwa/oauth/v1/applications/102/me/location" /> 
    ///    </sender> 
    ///  </events>
    ///  
    ///  2. Error or special case handling
    ///     a. Client sends out-of-order ack number (<earlest and >latest), server responds with a <link rel="resync" .../>
    ///         connection disrupted. clean and reactivate the app
    ///     b. App does not exist on server. UCWA return 404 ApplicationNotFound response. 
    ///         Clent can quit or recreate app with POST applications.
    ///     c. Client duplicate ack ID, the prior P-GET is released with a failure response (409/Conflict -- PGetReplace).
    ///         Client can ignore the response if it did the dup, otherwise, quit as another client is using the app.
    /// </summary>
    public sealed class UcwaAppEventChannel
    {
        string url;

        public UcwaAppTransport Transport { get; private set; }
        public UcwaAppEventNotificationsReceivedEventHandler OnEventNotificationsReceived;
        public UcwaAppEventChannelClosedEventHandler OnEventChannelClosed;
        public UcwaAppProgressReportEventHandler OnProgressReported;
        public UcwaAppErrorReportEventHandler OnErrorReported;

        public UcwaAppEventChannel(string channelUri, UcwaAppTransport transport) 
        {
            this.Transport = transport;
            this.url = channelUri;
        }

        Task workItem;
		CancellationTokenSource tokenSource;
        public void Start( )
        {
            var uri = this.url;
            UcwaAppOperationResult result;
			tokenSource = new CancellationTokenSource();

			workItem = Task.Run (async () => {
				while (!string.IsNullOrWhiteSpace(uri))
				{
					if (workItem.Status == TaskStatus.Canceled)
						break;

					// Make pending-Get request
					result = await this.Transport.GetResourceAsync(uri);
					if (result.StatusCode == HttpStatusCode.OK)
					{
						uri = GetNextEventUri(result.Resource);
						HandleEvent(result.Resource);
					}
					else
					{
						HandleException(result);
					}                                                    
				}
			}, tokenSource.Token);

			workItem.ContinueWith ((Task task) => {
				if (task.Status == TaskStatus.Canceled)
					return;

				if (OnEventChannelClosed != null) OnEventChannelClosed(task.Status);
			});
        }
        public void Stop()
        {
			if (workItem != null && tokenSource.Token.CanBeCanceled)
            {
				tokenSource.Cancel ();
            }
        }
        private void HandleException(UcwaAppOperationResult result)
        {
            switch (result.StatusCode)
            {
                case HttpStatusCode.RequestTimeout:
                    // request timed out, may need to readjust the default timeout value 
                    TaskCanceledException tcex = result.Exception as TaskCanceledException;
                    ReportError(tcex.Message);
                    break;
                case HttpStatusCode.Conflict:
                    // duped ack Ids
                    ReportError(result.StatusCode.ToString());
                    break;
                case HttpStatusCode.NotFound:
                    // app not exists on server
                    ReportError(result.StatusCode.ToString());
                    break;
                default:
                    ReportError(result.Exception.Message);
                    break;
            }
            this.Stop();
        }
        private void HandleEvent(UcwaResource resource)
        {
            if (OnEventNotificationsReceived != null)
                OnEventNotificationsReceived(new UcwaEventsData(resource.OuterXml));
        }
        private string GetNextEventUri(UcwaResource resource)
        {
            try
            {
                if (resource.LinkNames.Contains("resync"))
                    return resource.GetLinkUri("resync");
                if (resource.LinkNames.Contains("resume"))
                    return resource.GetLinkUri("resume");
                if (resource.LinkNames.Contains("next"))
                    return resource.GetLinkUri("next");
                return null;
            }
            catch { return null; }
        }

        private void ReportError(HttpStatusCode statusCode)
        {
            if (OnErrorReported != null)
                OnErrorReported(new Exception(statusCode.ToString()));
        }
        private void ReportError(string msg)
        {
            if (OnErrorReported != null)
                OnErrorReported(new Exception(msg));
        }

    }
}
