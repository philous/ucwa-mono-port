using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace WinStoreUcwaAppEvents
{
    public class UcwaAppMe : UcwaAppEventHandlers
    {
        public UcwaAppTransport Transport { get; set; }
        public string DisplayName { get { return this.Resource == null ? null : this.Resource.GetPropertyValue("name"); } }
        public string Title { get { return this.Resource == null ? null : this.Resource.GetPropertyValue("title"); } }
        public string Department { get { return this.Resource == null ? null : this.Resource.GetPropertyValue("department"); } }
        public string Uri { get { return this.Resource == null ? null : this.Resource.GetPropertyValue("uri"); } }
        public UcwaResource Resource { get; private set; }
        public UcwaResource Note { get; private set; }
        public UcwaResource Presence { get; private set; }
        public UcwaResource Phones { get; private set; }
        public UcwaResource Photo { get; private set; }
        public UcwaResource Location { get; private set; }

        UcwaApp ucwaApp;
        bool makeMeAvailablePosted = false;
        public UcwaAppMe(UcwaApp app)
        {
            this.ucwaApp = app;
            this.ucwaApp.OnEventNotificationsReceived += this.DispatchToUIThreadReceivedEventNotifications;
            this.ucwaApp.OnErrorReported += this.DispatchToUIThreadErrorReport;
            this.ucwaApp.OnProgressReported += this.DispatchToUIThreadProgressReport;
            this.Transport = app.Transport;
            this.Resource = app.ApplicationResource.GetEmbeddedResource("me");
        }

        //private override async void OnEventNotificationsReceivedHandler(UcwaEventsData events)
        //{
        //    if (events == null)
        //        return;
        //    if (this.OnEventNotificationsReceived != null)
        //        await UcwaAppUtils.DispatchEventToUI(CoreDispatcherPriority.Normal,
        //            new DispatchedHandler(() => { this.OnEventNotificationsReceived(events); }));

        //    foreach (var sender in events.SenderNames)
        //    {
        //        if (OnEventsReceived != null)
        //            await UcwaAppUtils.DispatchEventToUI(CoreDispatcherPriority.Normal,
        //                new DispatchedHandler(() => { OnEventsReceived(sender, eventsData.GetEventsBySender(sender)); }));
        //    }

        //}
        public async Task<UcwaAppOperationResult> PostMakeMeAvailable(string phoneNumber, string signInAs,
            string[] supportedMessageFormats, string[] supportedModalities)
        {
            string makeMeAvailableUri = this.Resource.GetLinkUri("makeMeAvailable");
            var requestData =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<input xmlns=\"http://schemas.microsoft.com/rtc/2012/03/ucwa\">" +
                "  <property name=\"phoneNumber\">" + phoneNumber + "</property>" +
                "  <property name=\"signInAs\">" + signInAs + "</property>";
            if (supportedMessageFormats != null)
            {
                requestData += " <propertyList name=\"supportedMessageFormats\">";
                foreach (var format in supportedMessageFormats)
                    requestData += "    <item>" + format + "</item>";
                requestData += "  </propertyList>";
            }
            if (supportedModalities != null)
            {
                requestData += "  <propertyList name=\"supportedModalities\">";
                foreach (var modality in supportedModalities)
                    requestData += "    <item>" + modality + "</item>";
                requestData += "  </propertyList>";
            }
            requestData += "</input>";

            var result = await Transport.PostResourceAsync(makeMeAvailableUri, requestData);
            if (result.StatusCode == HttpStatusCode.NoContent || result.StatusCode == HttpStatusCode.OK)
                makeMeAvailablePosted = true;
            return result;
        }
        public async Task<UcwaResource> Refresh(string uri=null)
        {
            if (string.IsNullOrEmpty(uri))
                this.Resource = await GetResource(this.Resource.Uri);
            else
                this.Resource= await GetResource(uri);
            this.Note = await this.GetNote();
            this.Presence = await this.GetPresence();
            this.Location = await this.GetLocation();
            this.Phones = await this.GetPhones();
            return this.Resource;
        }
        public async Task<HttpStatusCode> SetNote(string msg, string noteUri = null)
        {
            string inputFormat = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                                 "<input xmlns=\"http://schemas.microsoft.com/rtc/2012/03/ucwa\">" +
                                 "  <property name=\"message\">{0}</property>" +
                                 "</input>";
            var requestData = string.Format(inputFormat, msg);
            if (noteUri == null)
                return await SetResource(requestData, "note", this.Resource);
            return await SetResource(requestData, noteUri);
        }
        public async Task<UcwaResource> GetNote(string noteUri=null)
        {
            if (noteUri == null)
                this.Note =  await GetResource("note", this.Resource);
            else
                this.Note = await GetResource(noteUri);
            return this.Note;
        }
        public async Task<string> GetNoteMessage(string uri=null)
        { 
            this.Note = await this.GetNote(uri);
            return this.Note.GetPropertyValue("message"); 
        }
        public async Task<string> GetNoteType(string uri= null)
        { 
            this.Note = await GetNote(uri);
            return this.Note.GetPropertyValue("type"); 
        } 
        public async Task<UcwaResource> GetPresence(string presenceUri = null)
        {
            if (!makeMeAvailablePosted) return null;
            if (presenceUri == null)
                return await GetResource("presence", this.Resource);
            else
                return await GetResource(presenceUri);
        }
        public async Task<HttpStatusCode> SetPresence(string availability, string presenceUri = null)
        {
            string inputFormat = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                                 "<input xmlns=\"http://schemas.microsoft.com/rtc/2012/03/ucwa\">" +
                                 "  <property name=\"availability\">{0}</property>" +
                                 "</input>";
            var requestData = string.Format(inputFormat, availability);
            if (presenceUri == null)
                return await SetResource(requestData, "presence", this.Resource);
            else
                return await SetResource(requestData, presenceUri);
        }
        public async Task<string> GetPresenceAvailability(string uri= null) 
        { 
            this.Presence = await GetPresence(uri);
            return this.Presence.GetPropertyValue("availability"); 
        }
        public async Task<string> GetPresenceActivity (string uri=null)
        { 
            this.Presence = await GetPresence(uri);
            return this.Presence.GetPropertyValue("activity"); 
        }

        public async Task<UcwaResource> GetPhones(string phonesUri = null)
        {
            if (!makeMeAvailablePosted) return null;
            if (string.IsNullOrEmpty(phonesUri))
                this.Phones = await GetResource("phones", this.Resource);
            else
                this.Phones = await GetResource(phonesUri);
            return this.Phones;
        }
        public async Task<UcwaResource> GetLocation(string uri = null)
        {
            if (!makeMeAvailablePosted) return null;
            if (string.IsNullOrEmpty(uri))
                return await GetResource("location", this.Resource);
            else
                return await GetResource(uri);
        }
        public async Task<string> GetLocationCoordinates(string uri=null)
        { 
            this.Location = await GetLocation(uri);
            return Location.GetPropertyValue("location"); 
        }
        public async Task<UcwaResource> GetPhoto(string uri=null)
        {
            if (string.IsNullOrEmpty(uri))
                this.Photo = await GetResource("photo", this.Resource);
            else
                this.Photo = await GetResource(uri);
            return this.Photo;
        }
        //public async Task<Stream> GetPhotoStream(string photoUri = null)
        //{
        //    if (!makeMeAvailablePosted) return null;
        //    if (photoUri == null)
        //        photoUri = this.Resource.GetEmbeddedResourceUri("photo");
        //    if (string.IsNullOrEmpty(photoUri))
        //        return null;
        //    //var result = await Transport.GetResourceAsync(photoUri);
        //    this.Photo = await GetPhotoStream(photoUri);//GetResource(photoUri);
        //    return this.Photo ;
        //}
        public async Task<IEnumerable<UcwaAppPhoneLine>> GetPhoneLines(string uri=null)
        {
            this.Phones = await GetPhones(uri);
            List<UcwaAppPhoneLine> phoneList = new List<UcwaAppPhoneLine>();
            foreach (var xElem in this.Phones.EmbeddedResourceElements)
            {
                var resPhone = new UcwaResource(xElem);
                var phoneType = resPhone.GetPropertyValue("type");
                var phoneNumber = resPhone.GetPropertyValue("number");
                var inConactCard = resPhone.GetPropertyValue("includeInContactCard");
                phoneList.Add(new UcwaAppPhoneLine(phoneNumber, phoneType, inConactCard));
            }
            return phoneList.AsEnumerable();

        }

        private async Task<UcwaResource> GetResource(string resourceName, UcwaResource parent) 
        {
            if (string.IsNullOrEmpty(resourceName)) return null;
            if (parent == null) return null;

            var uri = parent.GetEmbeddedResourceUri(resourceName);
            return await GetResource(uri);
        }
        private async Task<UcwaResource> GetResource(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            var result = await Transport.GetResourceAsync(uri);
            return result.Resource;
        }
        private async Task<HttpStatusCode> SetResource(string data, string name, UcwaResource parent)
        {
            if (string.IsNullOrEmpty(name) || parent == null) return HttpStatusCode.BadRequest;
            return await SetResource(data, parent.GetEmbeddedResourceUri(name));
        }
        private async Task<HttpStatusCode> SetResource(string data, string uri)
        {
            if (string.IsNullOrEmpty(uri)) return HttpStatusCode.BadRequest;
            var result = await Transport.PostResourceAsync(uri, data);
            return result.StatusCode;
        }
        private async Task<Stream> GetPhotoStream(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            return await Transport.Client.GetStreamAsync(uri);

        }
    }

    public class UcwaAppPhoneLine
    {
        public string Type { get; private set; }
        public string Number { get; private set; }
        public string InContactCard { get; private set; }
        public UcwaAppPhoneLine(string number, string type, string inConactCard)
        {
            this.Type = type;
            this.Number = number;
            this.InContactCard = inConactCard;
        }
    }
}
