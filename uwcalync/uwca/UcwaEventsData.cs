using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WinStoreUcwaAppEvents
{
    public class UcwaEventsData : UcwaResource
    {
        public UcwaEventsData(string xmlBlock) : base(xmlBlock) { }
        public UcwaEventsData(XElement xElem) : base(xElem) {}
        public UcwaEventsData(Stream xmlStream) : base(xmlStream) { }
        public IEnumerable<string> SenderNames { get { return from res in this.SenderElements select res.Attribute("rel").Value; } }
        public IEnumerable<XElement> SenderElements {get { return xElem.Elements().Where(r => r.Name.LocalName == "sender"); }}
        public string GetSenderUri(string senderName)
        {
            var uri = GetLinkUri(senderName);
            if (string.IsNullOrEmpty(uri))
                uri = (from res in this.EmbeddedResourceElements where res.Attribute("rel").Value == senderName select res.Attribute("href").Value)
                    .FirstOrDefault();
            return uri;
        }
        public XElement GetSenderElement(string resourceName)
        {
            return (from res in this.SenderElements where res.Attribute("rel").Value == resourceName select res).FirstOrDefault();
        }
        
        public IEnumerable<UcwaEvent> GetEventsBySender(string senderName)
        {
            List<UcwaEvent> eventList = new List<UcwaEvent>();

            var sender = this.GetSenderElement(senderName);
            if (sender != null)
            {
                foreach(var e in sender.Elements().Where(e=>e.Name.LocalName=="added" || e.Name.LocalName == "updated" || e.Name.LocalName == "deleted"))
                {
                    eventList.Add(new UcwaEvent(e));
                }
            }                
            return eventList.AsEnumerable();
        }
    }

    public class UcwaEvent
    {
        XElement xElem = null;
        public UcwaEvent(XElement xElem) 
        { 
                this.xElem = xElem; 
        }

        public string Type { get { return xElem.Name.LocalName; } }
        public string Name { get { return xElem.Attributes("rel").Select(s => s.Value).FirstOrDefault(); } }
        public string Uri { get { return xElem.Attributes("href").Select(s => s.Value).FirstOrDefault(); } }

        public UcwaResource Resource
        {
            get
            {
                List<UcwaResource> resList = new List<UcwaResource>();
                foreach(var res in xElem.Elements().Where(e => e.Name.LocalName == "resource"))
                {
                    resList.Add(new UcwaResource(res));
                }
                return resList.FirstOrDefault();
            }
        }
    }
}
