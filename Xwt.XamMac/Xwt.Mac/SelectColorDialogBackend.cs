﻿//
// SelectColorDialogBackend.cs
//
// Author:
//       David Karlaš <david.karlas@gmail.com>
//
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
using System.ComponentModel;
using System.IO;
using Xwt.Backends;
using Xwt.Drawing;
using System.Runtime.InteropServices;
#if MONOMAC
using MonoMac.AppKit;
using MonoMac.Foundation;
using CGRect = System.Drawing.RectangleF;
#else
using AppKit;
using Foundation;
using CoreGraphics;
#endif

namespace Xwt.Mac
{
	public class SelectColorDialogBackend : ISelectColorDialogBackend
	{
		private NSColorPanel colorPanel;
		private Action<Color> callback;
		private NSObject observer;
		private Color color;

		public SelectColorDialogBackend()
		{
			colorPanel = NSColorPanel.SharedColorPanel;
		}

		// Important note: The dialog was originally modaless. It was switched to be modal.
		//				   You can see the file history in GIT to see the changes. (Eugene) 


		public bool Run(IWindowFrameBackend parent, string title, bool supportsAlpha, Action<Color> colorChangedCallback)
		{
			colorPanel.Delegate = new SelectColorDialogDelegate();
			colorPanel.ShowsAlpha = supportsAlpha;

			this.callback = colorChangedCallback;
			colorPanel.AnimationBehavior = NSWindowAnimationBehavior.None;

#if !MONOMAC // NSColorPanel.ColorChangedNotification is not defined for MonoMac
			observer = NSNotificationCenter.DefaultCenter.AddObserver(NSColorPanel.ColorChangedNotification, OnColorChanged);
#endif

			NSApplication.SharedApplication.RunModalForWindow(colorPanel);

			return true;
		}

		void OnColorChanged(NSNotification notification) 
		{
			this.Color = colorPanel.Color.ToXwtColor();
			callback.Invoke(this.Color);
		}

		public void Close() {
#if !MONOMAC
			NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
#endif
			this.colorPanel.Close();
		}

		public Color Color { 
			get
			{
				return color;
			}
			set 
			{ 
				this.color = value;
				colorPanel.Color = value.ToNSColor();
			}
		}

		public Size Size {
			get {
				return new Size(colorPanel.Frame.Width, colorPanel.Frame.Height);
			}
		}

		public Point ScreenPosition {
			set {
				// As long as the dilog is modal, we can not set its position with "SetFrame".
				Rectangle r = MacDesktopBackend.ToDesktopRect(new CGRect((float)value.X, (float)value.Y, colorPanel.Frame.Width, colorPanel.Frame.Height));
				colorPanel.SetFrame(r.ToCGRect(), true);
			}
		}
	}

	class SelectColorDialogDelegate : NSWindowDelegate {
		public override void WillClose(NSNotification notification) {
			NSApplication.SharedApplication.StopModal();
		}
	}
}
