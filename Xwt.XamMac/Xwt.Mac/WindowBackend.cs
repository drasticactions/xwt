// 
// WindowBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//       Andres G. Aragoneses <andres.aragoneses@7digital.com>
//       Konrad M. Kruczynski <kkruczynski@antmicro.com>
// 
// Copyright (c) 2011 Xamarin Inc
// Copyright (c) 2012 7Digital Media Ltd
// Copyright (c) 2016 Antmicro Ltd
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
using System.Collections.Generic;
using System.Linq;
using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using Xwt.Backends;
using Xwt.Drawing;
using NativeHandle = System.IntPtr;

namespace Xwt.Mac
{
	public class WindowBackend: NSWindow, IWindowBackend, IMacWindowBackend, INSWindowDelegate
	{
		WindowBackendController controller;
		IWindowFrameEventSink eventSink;
		Window frontend;
		ViewBackend child;
		NSView childView;
		bool sensitive = true;
		WindowFrameEvent eventsEnabled;

		public WindowBackend (NativeHandle ptr): base (ptr)
		{
			this.WillEnterFullScreen += HandleWindowStateChanging;
			this.WillMiniaturize += HandleWindowStateChanging;
			this.WillStartLiveResize += HandleWindowStateChanging;
		}

		private void HandleWindowStateChanging(object sender, EventArgs e) {
			if(this.WindowState == WindowState.Normal) {
				Rectangle restoreBounds = new Rectangle(this.Frame.X, this.Frame.Y, this.Frame.Width, this.Frame.Height);
				restoreBounds.Y = Desktop.Bounds.Height - (restoreBounds.Y + restoreBounds.Height); //Invert Y
				cachedRestoreBounds = restoreBounds;
			}
		}
		
		public WindowBackend ()
		{
			this.WillEnterFullScreen += HandleWindowStateChanging;
			this.WillMiniaturize += HandleWindowStateChanging;
			this.WillStartLiveResize += HandleWindowStateChanging;
			this.controller = new WindowBackendController ();
			controller.Window = this;
			StyleMask |= NSWindowStyle.Resizable | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable;
			AutorecalculatesKeyViewLoop = true;

			ContentView.AutoresizesSubviews = true;
			ContentView.Hidden = true;

			// TODO: do it only if mouse move events are enabled in a widget
			// Sept 10, 2023: Removed call to set AcceptsMouseMovedEvents = true
			// Seems to not have an adverse effect. Restore this if things
			// stop working
			//AcceptsMouseMovedEvents = true;

			Center();

			// Use event handlers instead of taking over WeakDelegate
			DidResize += HandleDidResize;
			DidMove += HandleDidMove;
			WindowShouldClose += HandleWindowShouldClose;
			WillClose += HandleWillClose;
		}

		object IWindowFrameBackend.Window {
			get { return this; }
		}

		public IntPtr NativeHandle {
			get { return Handle; }
		}

		public IWindowFrameEventSink EventSink {
			get { return (IWindowFrameEventSink)eventSink; }
		}

		public virtual void InitializeBackend (object frontend, ApplicationContext context)
		{
			this.ApplicationContext = context;
			this.frontend = (Window) frontend;
		}
		
		public void Initialize (IWindowFrameEventSink eventSink)
		{
			this.eventSink = eventSink;
		}
		
		public ApplicationContext ApplicationContext {
			get;
			private set;
		}
		
		public object NativeWidget {
			get {
				return this;
			}
		}

		public string Name { get; set; }

		void IMacWindowBackend.InternalShow ()
		{
			InternalShow ();
		}

		internal void InternalShow ()
		{
			MakeKeyAndOrderFront (MacEngine.App);

			if (weakParentWindow?.TryGetTarget(out var nsWindow) ?? false)
			{
				// always use NSWindow for alignment when running in guest mode and
				// don't rely on AddChildWindow to position the window correctly

				if (frontend.InitialLocation == WindowLocation.CenterParent && !(nsWindow is WindowBackend))
				{
					var parentBounds = MacDesktopBackend.ToDesktopRect(ParentWindow.ContentRectFor(ParentWindow.Frame));
					var bounds = ((IWindowFrameBackend)this).Bounds;
					bounds.X = parentBounds.Center.X - (Frame.Width / 2);
					bounds.Y = parentBounds.Center.Y - (Frame.Height / 2);
					((IWindowFrameBackend)this).Bounds = bounds;
				}

				if (AccessibilityFocusedWindow == nsWindow)
				{
					AccessibilityFocusedWindow = this;
				}
			}
		}
		
		public void Present ()
		{
			InternalShow();
		}

		public bool Visible {
			get {
				return IsVisible;
			}
			set {
				if (value)
					MacEngine.App.ShowWindow(this);
				ContentView.Hidden = !value; // handle shown/hidden events
				IsVisible = value;
			}
		}

		public double Opacity {
			get { return AlphaValue; }
			set { AlphaValue = (float)value; }
		}

		Color IWindowBackend.BackgroundColor {
			get {
				return BackgroundColor.ToXwtColor ();
			}
			set {
				BackgroundColor = value.ToNSColor ();
			}
		}

		public bool Sensitive {
			get {
				return sensitive;
			}
			set {
				sensitive = value;
				if (child != null)
					child.UpdateSensitiveStatus (child.Widget, sensitive);
			}
		}

		public override bool CanBecomeKeyWindow {
			get {
				// must be overriden or borderless windows will not be able to become key
				return frontend.CanBecomeKey;
			}
		}
		
		public bool HasFocus {
			get {
				return IsKeyWindow;
			}
		}

		public bool FullScreen {
			get {
				if (MacSystemInformation.OsVersion < MacSystemInformation.Lion)
					return false;

				return (StyleMask & NSWindowStyle.FullScreenWindow) != 0;

			}
			set {
				if (MacSystemInformation.OsVersion < MacSystemInformation.Lion)
					return;

				if (value != ((StyleMask & NSWindowStyle.FullScreenWindow) != 0))
					ToggleFullScreen (null);
			}
		}

		object IWindowFrameBackend.Screen {
			get {
				return Screen;
			}
		}

		private Rectangle cachedRestoreBounds;

		public WindowState WindowState {
			get {
				if(this.IsMiniaturized) {
					return WindowState.Minimized;
				} else if(this.StyleMask.HasFlag(NSWindowStyle.FullScreenWindow)) {
					return WindowState.FullScreen;
				} else if(this.IsZoomed) {
					return WindowState.Maximized;
				} else {
					return WindowState.Normal;
				}
			}

			set {

				if(value == WindowState) { return; }

				switch(value) {
				case WindowState.Minimized:
					if(this.StyleMask.HasFlag(NSWindowStyle.FullScreenWindow)) {
						this.ToggleFullScreen(this);
					}
					this.Miniaturize(this);
					break;
				case WindowState.FullScreen:
					if(this.IsMiniaturized) {
						this.Deminiaturize(this);
					}
					if(!this.StyleMask.HasFlag(NSWindowStyle.FullScreenWindow)) {
						this.ToggleFullScreen(this);
					}
					break;
				case WindowState.Maximized:
					if(this.StyleMask.HasFlag(NSWindowStyle.FullScreenWindow)) {
						this.ToggleFullScreen(this);
					}
					if(this.IsMiniaturized) {
						this.Deminiaturize(this);
					}
					if(!this.IsZoomed) {
						this.Zoom(this);
					}
					break;
				case WindowState.Normal:
					if(this.StyleMask.HasFlag(NSWindowStyle.FullScreenWindow)) {
						this.ToggleFullScreen(this);
					}
					if(IsZoomed) {
						this.Zoom(this);
					}
					if(this.IsMiniaturized) {
						this.Deminiaturize(this);
					}
					break;
				default:
					throw new InvalidOperationException("Invalid window state: " + value);
				}
			}
		}
			
		public Rectangle RestoreBounds {
			get {
				return cachedRestoreBounds;
			}
		}

		#region IWindowBackend implementation
		void IBackend.EnableEvent (object eventId)
		{
			if (eventId is WindowFrameEvent) {
				var @event = (WindowFrameEvent)eventId;
				switch (@event) {
					case WindowFrameEvent.Hidden:
					case WindowFrameEvent.Shown:
						if (!VisibilityEventsEnabled())
						{
							ContentView.AddObserver(this, HiddenProperty, NSKeyValueObservingOptions.New, IntPtr.Zero);
						}
						break;
				}
				eventsEnabled |= @event;
			}
		}

		new void HandleDidResize (object sender, EventArgs eventArgs)
		{
			OnBoundsChanged();
		}

		new void HandleDidMove(object sender, EventArgs eventArgs)
		{
			OnBoundsChanged ();
		}

		new bool HandleWindowShouldClose(NSObject sender)
		{
			return closePerformed = RequestClose ();
		}

		new void HandleWillClose(object sender, EventArgs eventArgs)
		{
			OnHidden ();
			OnClosed ();
		}

		internal bool RequestClose ()
		{
			bool res = true;
			ApplicationContext.InvokeUserCode (() => res = eventSink.OnCloseRequested ());
			return res;
		}

		protected virtual void OnClosed ()
		{
			if (!disposing)
				ApplicationContext.InvokeUserCode (eventSink.OnClosed);
		}

		bool closePerformed;

		bool IWindowFrameBackend.Close ()
		{
			closePerformed = true;
			if ((StyleMask & NSWindowStyle.Titled) != 0 && (StyleMask & NSWindowStyle.Closable) != 0)
				PerformClose(this);
			else
				Close ();
			return closePerformed;
		}
		
		bool VisibilityEventsEnabled ()
		{
			return eventsEnabled.HasFlag(WindowFrameEvent.Hidden) || eventsEnabled.HasFlag(WindowFrameEvent.Shown);
		}

		NSString HiddenProperty {
			get { return new NSString ("hidden"); }
		}

		public override void ObserveValue (NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
		{
			if (keyPath.IsEqual (HiddenProperty) && ofObject.Equals (ContentView)) {
				if (ContentView.Hidden) {
					OnHidden ();
				} else {
					OnShown ();
				}
			}
		}

		void OnHidden ()
		{
			if (eventsEnabled.HasFlag (WindowFrameEvent.Hidden))
			{
				ApplicationContext.InvokeUserCode (eventSink.OnHidden);
			}
		}

		void OnShown ()
		{
			if (eventsEnabled.HasFlag (WindowFrameEvent.Shown))
			{
				ApplicationContext.InvokeUserCode (eventSink.OnShown);
			}
		}

		void IBackend.DisableEvent (object eventId)
		{
			if (eventId is WindowFrameEvent) {
				var @event = (WindowFrameEvent)eventId;
				eventsEnabled &= ~@event;
				switch (@event) {
					case WindowFrameEvent.Hidden:
					case WindowFrameEvent.Shown:
						if (!VisibilityEventsEnabled())
						{
							ContentView.RemoveObserver(this, HiddenProperty);
						}
						break;
				}
			}
		}

		protected virtual void OnBoundsChanged ()
		{
			LayoutWindow ();
			if (eventsEnabled.HasFlag(WindowFrameEvent.BoundsChanged))
			{
				ApplicationContext.InvokeUserCode(delegate
				{
					eventSink.OnBoundsChanged(((IWindowBackend)this).Bounds);
				});
			}
		}
		
		public override void BecomeMainWindow ()
		{
			base.BecomeMainWindow ();
			eventSink.OnBecomeMain ();
		}
		
		public override void BecomeKeyWindow ()
		{
			base.BecomeKeyWindow ();
			eventSink.OnBecomeKey ();
		}

		void IWindowBackend.SetChild (IWidgetBackend child)
		{
			if (this.child != null) {
				ViewBackend.RemoveChildPlacement (this.child.Widget);
				this.child.Widget.RemoveFromSuperview ();
				childView = null;
			}
			this.child = (ViewBackend) child;
			if (child != null) {
				childView = ViewBackend.GetWidgetWithPlacement (child);
				ContentView.AddSubview (childView);
				LayoutWindow ();
				childView.AutoresizingMask = NSViewResizingMask.HeightSizable | NSViewResizingMask.WidthSizable;
			}
		}
		
		public virtual void UpdateChildPlacement (IWidgetBackend childBackend)
		{
			var w = ViewBackend.SetChildPlacement (childBackend);
			LayoutWindow ();
			w.AutoresizingMask = NSViewResizingMask.HeightSizable | NSViewResizingMask.WidthSizable;
		}

		bool IWindowFrameBackend.Decorated {
			get {
				return (StyleMask & NSWindowStyle.Titled) != 0;
			}
			set {
				if (value)
					StyleMask |= NSWindowStyle.Titled;
				else
					StyleMask &= ~(NSWindowStyle.Titled | NSWindowStyle.Borderless);
			}
		}
		
		bool IWindowFrameBackend.ShowInTaskbar {
			get {
				return false;
			}
			set {
			}
		}

		protected WeakReference<NSWindow> weakParentWindow;

		void IWindowFrameBackend.SetTransientFor (IWindowFrameBackend window)
		{
			if (!((IWindowFrameBackend)this).ShowInTaskbar)
				StyleMask &= ~NSWindowStyle.Miniaturizable;

			if (window is NSWindow nsWindow)
			{
				weakParentWindow = new WeakReference<NSWindow>(nsWindow);
			}
			else
			{
				weakParentWindow = null;
			}
		}

		bool IWindowFrameBackend.Resizable {
			get {
				return (StyleMask & NSWindowStyle.Resizable) != 0;
			}
			set {
				if (value)
					StyleMask |= NSWindowStyle.Resizable;
				else
					StyleMask &= ~NSWindowStyle.Resizable;
			}
		}
		
		public void SetPadding (double left, double top, double right, double bottom)
		{
			LayoutWindow ();
		}

		void IWindowFrameBackend.Move (double x, double y)
		{
			var r = FrameRectFor (new CGRect ((nfloat)x, (nfloat)y, Frame.Width, Frame.Height));
			var dr = MacDesktopBackend.ToDesktopRect(r);
			SetFrame (new CGRect((nfloat)dr.X, (nfloat)dr.Y, (nfloat)dr.Width, (nfloat)dr.Height), true);
		}
		
		void IWindowFrameBackend.SetSize (double width, double height)
		{
			var cr = ContentRectFor (Frame);
			if (width == -1)
				width = cr.Width;
			if (height == -1)
				height = cr.Height;
			var r = FrameRectFor (new CGRect ((nfloat)cr.X, (nfloat)cr.Y, (nfloat)width, (nfloat)height));

			// preserve window location, FrameRectFor will not adjust the left-bottom corner automatically
			var oldFrame = Frame;
			if (!oldFrame.IsEmpty) {
				r.Y = (oldFrame.Y + oldFrame.Height) - r.Height;
			}

			SetFrame (r, true);
			LayoutWindow ();
		}
		
		Rectangle IWindowFrameBackend.Bounds {
			get {
				var b = ContentRectFor (Frame);
				var r = MacDesktopBackend.ToDesktopRect (b);
				return new Rectangle ((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
			}
			set {
				CGRect cgr = MacDesktopBackend.FromDesktopRect(value);
				CGRect fr = FrameRectFor (cgr);
				SetFrame (fr, true);
			}
		}
		
		public void SetMainMenu (IMenuBackend menu)
		{
			var m = (MenuBackend) menu;
			m.SetMainMenuMode ();
			NSApplication.SharedApplication.Menu = m;
			
//			base.Menu = m;
		}
		
		#endregion

		static Selector closeSel = new Selector ("close");

		static readonly bool XamMacDangerousDispose = Version.Parse(Constants.Version) < new Version(5, 6);

		public static bool SetReleasedWhenClosed = true;

		bool disposing, disposed;

		protected override void Dispose(bool disposing)
		{
			if (!disposed && disposing)
			{
				this.disposing = true;
				try
				{
					if(VisibilityEventsEnabled() && ContentView != null)
						ContentView.RemoveObserver(this, HiddenProperty);

					// Disable the following hack - not really sure what it is supposed to accomplish but when it is here,
					// some dialog windows cause a crash when they are disposed. Exclusion list avoids this problem. WIN-5549
					if(SetReleasedWhenClosed) {
						if(XamMacDangerousDispose) {
							// HACK: Xamarin.Mac/MonoMac limitation: no direct way to release a window manually
							// A NSWindow instance will be removed from NSApplication.SharedApplication.Windows
							// only if it is being closed with ReleasedWhenClosed set to true but not on Dispose
							// and there is no managed way to tell Cocoa to release the window manually (and to
							// remove it from the active window list).
							// see also: https://bugzilla.xamarin.com/show_bug.cgi?id=45298
							// WORKAROUND:
							// bump native reference count by calling DangerousRetain()
							// base.Dispose will now unref the window correctly without crashing
							DangerousRetain();
						}
						// tell Cocoa to release the window on Close
						ReleasedWhenClosed = true;
					}

					// Close the window (Cocoa will do its job even if the window is already closed)
					Messaging.void_objc_msgSend (this.Handle, closeSel.Handle);
				} finally {
					this.disposing = false;
					this.disposed = true;
				}
			}
			if (controller != null) {
				controller.Dispose ();
				controller = null;
			}
			base.Dispose (disposing);
		}
		
		public void DragStart (TransferDataSource data, DragDropAction dragAction, object dragImage, double xhot, double yhot)
		{
			throw new NotImplementedException ();
		}
		
		public void SetDragSource (string[] types, DragDropAction dragAction)
		{
		}
		
		public void SetDragTarget (string[] types, DragDropAction dragAction)
		{
		}
		
		public virtual void SetMinSize (Size s)
		{
			var b = ((IWindowBackend)this).Bounds;
			if (b.Size.Width < s.Width)
				b.Width = s.Width;
			if (b.Size.Height < s.Height)
				b.Height = s.Height;

			if (b != ((IWindowBackend)this).Bounds)
				((IWindowBackend)this).Bounds = b;

			var r = FrameRectFor (new CGRect (0, 0, (nfloat)s.Width, (nfloat)s.Height));
			MinSize = r.Size;
		}

		public void SetIcon (ImageDescription icon)
		{
		}
		
		public virtual void GetMetrics (out Size minSize, out Size decorationSize)
		{
			minSize = decorationSize = Size.Zero;
		}

		public virtual void LayoutWindow ()
		{
			LayoutContent (ContentView.Frame);
		}
		
		public void LayoutContent (CGRect frame)
		{
			if (child != null) {
				frame.X += (nfloat) frontend.Padding.Left;
				frame.Width -= (nfloat) (frontend.Padding.HorizontalSpacing);
				frame.Y += (nfloat) (childView.IsFlipped ? frontend.Padding.Bottom : frontend.Padding.Top);
				frame.Height -= (nfloat) (frontend.Padding.VerticalSpacing);
				childView.Frame = frame;
			}
		}
	}
	
	public partial class WindowBackendController : NSWindowController
	{
		public WindowBackendController ()
		{
		}
	}
}

