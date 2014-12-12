using System;

using System.Net;

namespace WinStoreUcwaAppEvents
{
    public class UcwaAppEventHandlers
    {
        public UcwaAppEventNotificationsReceivedEventHandler OnEventNotificationsReceived;   // delegate for parsing the events on the calling (UI) thread
        public UcwaAppProgressReportEventHandler OnProgressReported; // delegate for reporting progress on the calling UI thread
        public UcwaAppErrorReportEventHandler OnErrorReported; // delegate for reporting errors on the callling UI thread

        public virtual void ForwardEventNotificationsReceived(UcwaEventsData events)
        {
            if (OnEventNotificationsReceived != null)
            {
                OnEventNotificationsReceived(events);
            }
        }
        public virtual void ForwardReportedProgress(string msg, HttpStatusCode statusCode)
        {
            if (OnProgressReported != null)
                OnProgressReported(msg, statusCode);
        }
        public virtual void ForwardReportedErrors(Exception e)
        {
            if (OnErrorReported != null)
                OnErrorReported(e);
        }
        protected virtual void DispatchToUIThreadReceivedEventNotifications(UcwaEventsData events)
        {
            if (this.OnEventNotificationsReceived != null)
                UcwaAppUtils.DispatchEventToUI(() => this.OnEventNotificationsReceived (events));

            //foreach (var sender in eventsData.SenderNames)
            //{
            //    if (OnEventsReceived != null)
            //        await UcwaAppUtils.DispatchEventToUI(CoreDispatcherPriority.Normal,
            //            new DispatchedHandler(() => { OnEventsReceived(sender, eventsData.GetEventsBySender(sender)); }));
            //}
        }
        protected virtual void DispatchToUIThreadErrorReport(Exception e)
        {
            if (OnErrorReported != null)
                UcwaAppUtils.DispatchEventToUI(() => OnErrorReported (e));
        }
        protected virtual void DispatchToUIThreadProgressReport(string msg, HttpStatusCode status)
        {
            if (OnProgressReported != null)
                UcwaAppUtils.DispatchEventToUI(() => OnProgressReported (msg, status));
        }

    }
}
