//
// MacClipboardBackend.cs
//
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Linq;
using AppKit;
using Foundation;
using Xwt.Backends;

using Class = ObjCRuntime.Class;

namespace Xwt.Mac
{
	public class MacClipboardBackend: ClipboardBackend
	{
		PasteboardOwner owner;

		public MacClipboardBackend ()
		{
			owner = new PasteboardOwner ();
		}

		#region implemented abstract members of ClipboardBackend

		public override void Clear ()
		{
			NSPasteboard.GeneralPasteboard.ClearContents ();
		}

		public override void SetData (TransferDataType type, Func<object> dataSource, bool cleanClipboardFirst = true)
		{
			var pboard = NSPasteboard.GeneralPasteboard;
			if(cleanClipboardFirst) {
				pboard.ClearContents();
			}
			owner.DataSource = dataSource;

			pboard.AddTypes (new[] { type.ToUTI () }, owner);
		}

		public override bool IsTypeAvailable (TransferDataType type)
		{ 
			NSPasteboard pb = NSPasteboard.GeneralPasteboard;
			Class[] classes;
			NSDictionary options;
			bool isType;

			if (pb.PasteboardItems.Length == 0) {
				return false;
			}

			if (type == TransferDataType.Image) {
				//The below only works for images copied from web browsers, doesn't work for raw images.

//				NSObject urlClassObj = NSObject.FromObject(new MonoMac.ObjCRuntime.Class(typeof(NSUrl)));
//				NSObject imageClassObj = NSObject.FromObject(new MonoMac.ObjCRuntime.Class(typeof(NSImage)));
//				classes = new NSObject[]{ urlClassObj };
//				NSObject a = new NSString(type.ToUTI());
//				options = NSDictionary.FromObjectAndKey(imageClassObj, a);
//				isType = pb.CanReadObjectForClasses(classes, options);
//				return isType;
				var item = pb.PasteboardItems[0];
				foreach (string itemType in item.Types) {
					if (itemType == "public.tiff" || itemType == "public.png") {
						return true;
					}
				}
				return false;
			} else if (type == TransferDataType.Text) {
				// text
				var item = pb.PasteboardItems[0];
				foreach (string itemType in item.Types) {
					if (itemType == "public.file-url") {
						return true;
					}
				}

				classes = new Class[] {
					new Class(typeof(NSAttributedString)),
					new Class(typeof(NSString)),
					new Class(typeof(NSUrl)),
				};
				options = new NSDictionary();
				isType = pb.CanReadObjectForClasses(classes, options);
				return isType;
			} else if (type == TransferDataType.Uri) {
				//files
				classes = new Class[]{ new Class(typeof(NSUrl)) };
				options = NSDictionary.FromObjectAndKey(NSObject.FromObject(NSNumber.FromBoolean(true)), new NSString(type.ToUTI()));
				isType = pb.CanReadObjectForClasses(classes, options);
				return isType;
			}
			return NSPasteboard.GeneralPasteboard.Types.Contains (type.ToUTI ());
		}

		public override object GetData (TransferDataType type)
		{
			
			if (type == TransferDataType.Uri) {
				NSPasteboard pasteBoard = NSPasteboard.GeneralPasteboard;
				NSArray nsArray = (NSArray)pasteBoard.GetPropertyListForType(NSPasteboard.NSFilenamesType);
				NSString[] pathArray = NSArray.FromArray<NSString>(nsArray);
				if(pathArray != null) {
					string[] uriArray = new string[pathArray.Length];
					for(int i = 0; i < pathArray.Length; i++) {
						Uri fileUrl = new Uri(pathArray[i].ToString());
						if(fileUrl != null && fileUrl.IsFile) {
							uriArray[i] = pathArray[i].ToString();
						}
					}
					return uriArray;
				}
			}

			if(type == TransferDataType.Image) {
				NSPasteboard pasteBoard = NSPasteboard.GeneralPasteboard;
				string[] imageTypes = NSImage.ImageUnfilteredPasteboardTypes();
				for (int i = 0; i< imageTypes.Length; i++) {
					NSData imgData = pasteBoard.GetDataForType(imageTypes[i]);
					if(imgData != null) {
						NSImage nsImg = new NSImage(imgData);
						return ApplicationContext.Toolkit.WrapImage (nsImg);
					}
				}
			}

			// Url as text!
			if (type == TransferDataType.Text) {
				NSUrl url = NSUrl.FromPasteboard(NSPasteboard.GeneralPasteboard);
				if(url != null && url.IsFileUrl) {
					return "file://" + new Uri(url.Path).AbsolutePath;
				}
			}

			var data = NSPasteboard.GeneralPasteboard.GetDataForType (type.ToUTI ());
			if (data == null)
				return null;

			if (type == TransferDataType.Text)
				return data.ToString ();
			if (type == TransferDataType.Image)
				return ApplicationContext.Toolkit.WrapImage (new NSImage (data));

			unsafe {
				var bytes = new byte [data.Length];
				using (var stream = new UnmanagedMemoryStream ((byte*)data.Bytes, bytes.Length))
					stream.Read (bytes, 0, bytes.Length);
				try {
					return TransferDataSource.DeserializeValue (bytes, TransferDataType.ToType(type.Id));
				} catch(System.Runtime.Serialization.SerializationException) {
					// if data cannot be read, do not crash - return null
					return null;
				}
			}
		}

		public override IAsyncResult BeginGetData (TransferDataType type, AsyncCallback callback, object state)
		{
			throw new NotImplementedException ();
		}

		public override object EndGetData (IAsyncResult ares)
		{
			throw new NotImplementedException ();
		}

		#endregion
	}

	[Register ("XwtPasteboardOwner")]
	class PasteboardOwner : NSObject
	{
		public Func<object> DataSource {
			get;
			set;
		}

		[Export ("pasteboard:provideDataForType:")]
		public void ProvideData (NSPasteboard pboard, NSString type)
		{
			NSData data;
			var obj = DataSource ();
			if (obj is Xwt.Drawing.Image) {
				var bmp = ((Xwt.Drawing.Image)obj).ToBitmap ();
				data = ((NSImage)Toolkit.GetBackend (bmp)).AsTiff ();
			}
			else if (obj is NSImage)
				data = ((NSImage)obj).AsTiff ();
			else if (obj is Uri)
				data = NSData.FromUrl ((NSUrl)((Uri)obj));
			else if (obj is string)
				data = NSData.FromString ((string)obj);
			else
				data = NSData.FromArray (TransferDataSource.SerializeValue (obj, obj.GetType()));
			pboard.SetDataForType (data, type);
		}
	}
}

