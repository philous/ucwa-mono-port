using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace WinStoreUcwaAppEvents
{
    public delegate void UcwaAppEventNotificationsReceivedEventHandler(UcwaEventsData events);
	public delegate void UcwaAppEventChannelClosedEventHandler(TaskStatus status);
    public delegate void UcwaAppEventsReceivedEventHandler(string sender, IEnumerable<UcwaEvent> events);
    public delegate void UcwaAppProgressReportEventHandler(string message, HttpStatusCode status = HttpStatusCode.OK);
    public delegate void UcwaAppErrorReportEventHandler(Exception e);
}
