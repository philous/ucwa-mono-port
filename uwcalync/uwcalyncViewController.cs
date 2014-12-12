using System;
using System.Drawing;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using WinStoreUcwaAppEvents;

namespace uwcalync
{
	public partial class uwcalyncViewController : UIViewController
	{
		public uwcalyncViewController (IntPtr handle) : base (handle)
		{
		}

		public override void DidReceiveMemoryWarning ()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning ();
			
			// Release any cached data, images, etc that aren't in use.
		}

		#region View lifecycle

		UcwaApp UcwaApp;

		void ProcessEventNotifications (UcwaEventsData events)
		{
			Console.WriteLine ("events");
		}

		void ReportError (Exception e)
		{
			Console.WriteLine ("error " + e.ToString());
		}

		void ReportProgress (string message, System.Net.HttpStatusCode status)
		{

			Console.WriteLine ("progress" + " " + message + " " + status.ToString());
		}

		public async override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			
			this.UcwaApp = new UcwaApp();
			this.UcwaApp.OnEventNotificationsReceived += this.ProcessEventNotifications;
			this.UcwaApp.OnErrorReported += this.ReportError;
			this.UcwaApp.OnProgressReported += ReportProgress;

			await this.UcwaApp.SignIn(string.Empty, string.Empty);

			// Show some local user info.
			Console.WriteLine(this.UcwaApp.Me.DisplayName + ", " + this.UcwaApp.Me.Title + ", " + this.UcwaApp.Me.Department + ", " + this.UcwaApp.Me.Uri);

			Console.WriteLine(await this.UcwaApp.Me.GetNoteMessage());
			Console.WriteLine(await this.UcwaApp.Me.GetPresenceAvailability());
			var phones = await this.UcwaApp.Me.GetPhoneLines();
			var phonesText = string.Empty;
			foreach (var phone in phones)
				phonesText += (string.IsNullOrEmpty(phonesText) ? "" : ", ") + phone.Type + ":" + phone.Number;

			Console.WriteLine (phonesText);
		}

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
		}

		public override void ViewDidAppear (bool animated)
		{
			base.ViewDidAppear (animated);
		}

		public override void ViewWillDisappear (bool animated)
		{
			base.ViewWillDisappear (animated);
		}

		public override void ViewDidDisappear (bool animated)
		{
			base.ViewDidDisappear (animated);
		}

		#endregion
	}
}

