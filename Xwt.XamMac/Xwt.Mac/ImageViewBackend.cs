// 
// ImageViewBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2011 Xamarin Inc
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
using AppKit;
using Xwt.Backends;

namespace Xwt.Mac
{
	public class ImageViewBackend: ViewBackend<NSView,IWidgetEventSink>, IImageViewBackend
	{
		CustomAlignedContainer Container {
			get { return (CustomAlignedContainer)base.Widget; }
		}

		public new NSImageView Widget {
			get { return (NSImageView)Container.Child; }
		}

		public ImageViewBackend ()
		{
		}

		public override void Initialize ()
		{
			base.Initialize();
			ViewObject = new CustomAlignedContainer (EventSink, ApplicationContext, new NSImageView ()) {
				ExpandVertically = true
			};
		}

		protected override Size GetNaturalSize ()
		{
			return nsImage == null ? Size.Zero : nsImage.Size.ToXwtSize ();
		}

		NSImage nsImage; // Needed to keep a ref, otherwise Xam.Mac might GC the managed version and then try to resurrect it

		public void SetImage (ImageDescription image)
		{
			if (image.IsNull) {
				Widget.Image = null;
				nsImage = null;
				return;
			}
			
			nsImage = image.ToNSImage ();
			Widget.Image = nsImage;
			Widget.SetFrameSize (nsImage.Size);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing)
				nsImage = null;

			base.Dispose(disposing);
		}
	}
}

