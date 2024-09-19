﻿// 
// WindowBackend.cs
//  
// Author:
//       Carlos Alberto Cortez <calberto.cortez@gmail.com>
//       Luís Reis <luiscubal@gmail.com>
// 
// Copyright (c) 2011 Carlos Alberto Cortez
// Copyright (c) 2012 Luís Reis
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
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SW = System.Windows;

using Xwt.Backends;
using System.ComponentModel;

namespace Xwt.WPFBackend
{
	public class WindowBackend : WindowFrameBackend, IWindowBackend, IPopupWindowBackend, IUtilityWindowBackend
	{
		protected Grid rootPanel;
		public System.Windows.Controls.Menu mainMenu;
		MenuBackend mainMenuBackend;
		FrameworkElement widget;
		DockPanel contentBox;
		WindowStyle defaultWindowStyle = WindowStyle.SingleBorderWindow;

		public WindowBackend ()
		{
			base.Window = new WpfWindow ();

			base.Window.StateChanged += (object sender, EventArgs e) => {
				cachedRestoreBounds = new Rectangle(Window.RestoreBounds.X, Window.RestoreBounds.Y, Window.RestoreBounds.Width, Window.RestoreBounds.Height);
			};
			Window.UseLayoutRounding = true;
			rootPanel = CreateMainGrid ();
			contentBox = new DockPanel ();

			Window.Content = rootPanel;
			Grid.SetColumn (contentBox, 0);
			Grid.SetRow (contentBox, 1);
			rootPanel.Children.Add (contentBox);

			this.Window.Closing += (object sender, CancelEventArgs e) => {
				if(e.Cancel == false && this.Window.Owner != null) {
					this.Window.Owner.Focus();
				}
			};
		}

		new WpfWindow Window
		{
			get { return (WpfWindow)base.Window; }
		}

		bool initialized;

		public override void Initialize ()
		{
			if (!initialized) {
				base.Initialize();
				Window.Frontend = (Window)Frontend;
				if (Frontend is UtilityWindow)
					defaultWindowStyle = WindowStyle.ToolWindow;
				Decorated = true;
				initialized = true;
			}
		}

		public void Initialize(IWindowFrameEventSink sink, PopupWindow.PopupType type)
		{
			if (!initialized) {
				base.Initialize();
				Window.Frontend = (Window)Frontend;
				defaultWindowStyle = WindowStyle.ToolWindow;
				Decorated = false;
				initialized = true;
			}
		}

		// A Grid with a single column, and two rows (menu and child control).
		static Grid CreateMainGrid ()
		{
			var grid = new Grid ();

			grid.ColumnDefinitions.Add (new ColumnDefinition ());
			
			var menuRow = new RowDefinition () { Height = GridLength.Auto }; // Only take the menu requested space.
			var contentRow = new RowDefinition (); // Take all the remaining space (default).

			grid.RowDefinitions.Add (menuRow);
			grid.RowDefinitions.Add (contentRow);

			return grid;
		}

		public override bool Decorated
		{
			get { return base.Decorated; }
			set
			{
				base.Decorated = value;
				if (value)
					Window.WindowStyle = defaultWindowStyle;
				// don't attempt to change transparency unless necessary
				bool transparency = Window.WindowStyle == WindowStyle.None && BackgroundColor.Alpha < 1.0;
				if(transparency != Window.AllowsTransparency)
					Window.AllowsTransparency = transparency;
			}
		}

		public override bool HasMenu {
			get { return mainMenu != null; }
		}

		public bool FullScreen {
			get { return false; } // always false on windows
			set { }
		}

		public override Rectangle Bounds
		{
			get
			{
				Rectangle bounds = Window.ClientBounds;
				return new Xwt.Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
			}
			set
			{
				Window.ClientBounds = value;
				Context.InvokeUserCode (delegate
				{
					EventSink.OnBoundsChanged (Bounds);
				});
			}
		}

		public void SetChild (IWidgetBackend child)
		{
			if (widget != null) {
				contentBox.Children.Remove (widget);
				widget.SizeChanged -= ChildSizeChanged;
			}
			widget = ((IWpfWidgetBackend)child).Widget;
			contentBox.Children.Add (widget);

			// This event is subscribed to ensure that the content of the
			// widget is reallocated when the widget gets a new size. This
			// is not a problem when setting the child before showing the
			// window, but it may be a problem if the window is already visible.
			widget.SizeChanged += ChildSizeChanged;

			if (child != null)
				UpdateChildPlacement (child);
		}

		public virtual void UpdateChildPlacement (IWidgetBackend childBackend)
		{
			WidgetBackend.SetChildPlacement (childBackend);
		}

		void ChildSizeChanged (object o, SizeChangedEventArgs args)
		{
			((Window)Frontend).Content.Surface.Reallocate ();
		}

		public void SetMainMenu (IMenuBackend menu)
		{
			if (mainMenu != null) {
				mainMenuBackend.ParentWindow = null;
				rootPanel.Children.Remove (mainMenu);
			}
		
			if (menu == null) {
				mainMenu = null;
				mainMenuBackend = null;
				return;
			}

			var menuBackend = (MenuBackend)menu;

			var m = new System.Windows.Controls.Menu ();
			foreach (var item in menuBackend.Items)
				m.Items.Add (item.Item);

			Grid.SetColumn (m, 0);
			Grid.SetRow (m, 0);
			rootPanel.Children.Add (m);

			mainMenu = m;
			mainMenuBackend = menuBackend;
			mainMenuBackend.ParentWindow = this;
		}

		public void SetPadding (double left, double top, double right, double bottom)
		{
			contentBox.Margin = new Thickness (left, top, right, bottom);
		}

		public virtual void SetMinSize (Size s)
		{
			Window.SetMinSize (s);
		}

		public virtual void GetMetrics (out Size minSize, out Size decorationSize)
		{
			minSize = decorationSize = Size.Zero;
			if (mainMenu != null) {
				mainMenu.InvalidateMeasure ();
				mainMenu.Measure (new System.Windows.Size (double.PositiveInfinity, double.PositiveInfinity));
				var h = mainMenu.DesiredSize.Height;
				decorationSize.Height = h;
			}
		}

		protected override void OnResizeModeChanged ()
		{
			Window.ResetBorderSize ();
		}

		public Xwt.Drawing.Color BackgroundColor {
			get { return Window.Background.ToXwtColor (); }
			set {
				Window.Background = ResPool.GetSolidBrush (value);
				Window.AllowsTransparency = Window.WindowStyle == WindowStyle.None && value.Alpha < 1.0;
			}
		}
	}

	class WpfWindow : System.Windows.Window
	{
		public Window Frontend;

		bool borderCalculated;
		WidgetSpacing frameBorder;
		Size minSizeRequested;
		double initialX, initialY;

		public WpfWindow ()
		{
			// We initially use WidthAndHeight mode since we need to calculate the size
			// of the window borders
			SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
		}

		public void ResetBorderSize ()
		{
			// Called when the size of the border may have changed
			if (borderCalculated) {
				var r = ClientBounds;
				initialX = Left + frameBorder.Left;
				initialY = Top + frameBorder.Top;
				borderCalculated = false;
				ClientBounds = r;
			}
		}

		public void SetMinSize (Size size)
		{
			if(size.Height < 0 && size.Height != -1) {
				size.Height = 0;
			}
			if(size.Width < 0 && size.Width != -1) {
				size.Width = 0;
			}
			if (borderCalculated) {
				if (size.Width != -1)
					MinWidth = size.Width + frameBorder.HorizontalSpacing;
				if (size.Height != -1)
					MinHeight = size.Height + frameBorder.VerticalSpacing;
			}
			else
				minSizeRequested = size;
		}

		public Rectangle ClientBounds
		{
			get
			{
				var c = (FrameworkElement)Content;
				var w = double.IsNaN (c.Width) ? c.ActualWidth : c.Width;
				var h = double.IsNaN (c.Height) ? c.ActualHeight : c.Height;
				if (PresentationSource.FromVisual (c) == null)
					return new Rectangle (initialX, initialY, w, h);
				else {
					var p = c.PointToScreenDpiAware (new SW.Point (0, 0));
					return new Rectangle (p.X, p.Y, w, h);
				}
			}
			set
			{
				// Don't use WindowFrameBackend.ToNonClientRect to calculate the client area because that method is not reliable (see comment in ToNonClientRect).
				// Instead, we use our own border size calculation method, which is:
				// 1) Set the Width and Height of the widget to the desired client rect, and set SizeToContent property to WidthAndHeight
				// 2) The window will resize itself to fit the content
				// 3) When the size of the window is set (OnRenderSizeChanged event), calculate the border by comparing the screen position of
				//    the root content with the screen position of the window.

				if (borderCalculated) {
					// Border size already calculated. Just do the math.
					Left = value.Left - frameBorder.Left;
					Top = value.Top - frameBorder.Top;
					Width = value.Width + frameBorder.HorizontalSpacing;
					Height = value.Height + frameBorder.VerticalSpacing;
				}
				else {
					// store the required size and position and enable SizeToContent mode. When the window size is set, we'll calculate the border size.
					var c = (FrameworkElement)Content;
					initialX = value.Left;
					initialY = value.Top;
					c.Width = value.Width;
					c.Height = value.Height;
					SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
				}
			}
		}

		protected override System.Windows.Size ArrangeOverride (System.Windows.Size arrangeBounds)
		{
			var s = base.ArrangeOverride (arrangeBounds);
			if (Frontend.Content != null)
				Frontend.Content.Surface.Reallocate ();
			return s;
		}

		protected override void OnActivated(EventArgs e) {
			base.OnActivated(e);
			((WindowBackend)Frontend.GetBackend()).EventSink.OnBecomeKey();
		}

		protected override void OnRenderSizeChanged (SizeChangedInfo sizeInfo)
		{
			// Once the physical size of the window has been set we can calculate
			// the size of the borders, which will be used for further client/non client
			// area coordinate conversions
			CalcBorderSize (sizeInfo.NewSize.Width, sizeInfo.NewSize.Height);
			base.OnRenderSizeChanged (sizeInfo);
		}

		void CalcBorderSize (double windowWidth, double windowHeight)
		{
			if (borderCalculated)
				return;

			double left = 0;
			double top = 0;
			double right = 0;
			double bottom = 0;

			var c = (FrameworkElement)Content;

			if(PresentationSource.FromVisual(c) == null) {
				// HACK to avoid WIN-4661
				// This never happens in my testing, but seems to happen in the wild.
				// This change should avoid the outright failure to start and only has minimal functional
				// impact (Ex: Windows load a titlebar height lower than they should)
				Console.WriteLine("WARNING: PresentationSource.FromVisual returned null - border size calculations may be incorrect");
			} else {
				if(this.WindowStyle != SW.WindowStyle.None) {
					var p = c.PointToScreenDpiAware(new SW.Point(0, 0));
					left = p.X - Left;
					top = p.Y - Top;
					right = windowWidth - c.ActualWidth - left;
					bottom = windowHeight - c.ActualHeight - top;
				}
				borderCalculated = true;
			}

			frameBorder = new WidgetSpacing(left, top, right, bottom);

			Left = initialX - left;
			Top = initialY - top;
			SetMinSize (minSizeRequested);

			// Border size calculation done and we can go back to Manual resize mode.
			// From now on, the content has to adapt to the size of the window.
			SizeToContent = System.Windows.SizeToContent.Manual;
			c.Width = double.NaN;
			c.Height = double.NaN;
		}
	}
}
