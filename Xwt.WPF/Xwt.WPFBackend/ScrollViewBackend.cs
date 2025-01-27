// 
// ScrollViewBackend.cs
//  
// Author:
//	   Eric Maupin <ermau@xamarin.com>
// 
// Copyright (c) 2012 Xamarin, Inc.
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
using Xwt.Backends;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Markup;

namespace Xwt.WPFBackend
{
	public class ScrollViewBackend
		: WidgetBackend, IScrollViewBackend
	{
		private static readonly ResourceDictionary ScrollViewResourceDictionary;
		static ScrollViewBackend ()
		{
			Uri uri = new Uri("pack://application:,,,/Xwt.WPF;component/XWT.WPFBackend/ScrollView.xaml");
			ScrollViewResourceDictionary = (ResourceDictionary)XamlReader.Load(System.Windows.Application.GetResourceStream(uri).Stream);
		}

		void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
			((IScrollViewEventSink)EventSink).OnVisibleRectChanged();
		}

		public void ScrollToPoint(Point point) {
			this.ScrollViewer.ScrollToVerticalOffset(point.Y);
			this.ScrollViewer.ScrollToHorizontalOffset(point.X);
		}

		public ScrollViewBackend ()
		{
			ScrollViewer = new ExScrollViewer ();
			ScrollViewer.Resources.MergedDictionaries.Add (ScrollViewResourceDictionary);
			ScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
			ScrollViewer.GotKeyboardFocus += ScrollViewer_GotKeyboardFocus;
			ScrollViewer.LostKeyboardFocus += ScrollViewer_LostKeyboardFocus;
		}

		Brush previousBrush;
		Thickness previousBrushThickness;
		void ScrollViewer_LostKeyboardFocus (object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
		{
			if (BorderVisible) {
				ScrollViewer.BorderThickness = previousBrushThickness;
				ScrollViewer.BorderBrush = previousBrush;
			}
		}

		void ScrollViewer_GotKeyboardFocus (object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
		{
			if (BorderVisible) {
				previousBrush = ScrollViewer.BorderBrush;
				previousBrushThickness = ScrollViewer.BorderThickness;
				ScrollViewer.BorderThickness = new Thickness (1);
				ScrollViewer.BorderBrush = SystemColors.HighlightBrush;
			}
		}

		public ScrollPolicy VerticalScrollPolicy
		{
			get { return GetScrollPolicy (ScrollViewer.VerticalScrollBarVisibility); }
			set { ScrollViewer.VerticalScrollBarVisibility = GetScrollVisibility (value); }
		}

		public ScrollPolicy HorizontalScrollPolicy
		{
			get { return GetScrollPolicy (ScrollViewer.HorizontalScrollBarVisibility); }
			set { ScrollViewer.HorizontalScrollBarVisibility = GetScrollVisibility (value); }
		}

		IScrollControlBackend vscrollControl;
		public IScrollControlBackend CreateVerticalScrollControl()
		{
			if (vscrollControl == null)
				vscrollControl = new ScrollControlBackend(ScrollViewer, true);
			return vscrollControl;
		}

		IScrollControlBackend hscrollControl;
		public IScrollControlBackend CreateHorizontalScrollControl()
		{
			if (hscrollControl == null)
				hscrollControl = new ScrollControlBackend(ScrollViewer, false);
			return hscrollControl;
		}

		protected override double DefaultNaturalHeight
		{
			get
			{
				return VerticalScrollPolicy != ScrollPolicy.Never ? - 1 : -2;
			}
		}

		protected override double DefaultNaturalWidth
		{
			get
			{
				return HorizontalScrollPolicy != ScrollPolicy.Never ? -1 : -2;
			}
		}

		private bool borderVisible = true;
		public bool BorderVisible
		{
			get { return this.borderVisible; }
			set
			{
				if (this.borderVisible == value)
					return;

				if (value)
					ScrollViewer.ClearValue (Control.BorderBrushProperty);
				else
					ScrollViewer.BorderBrush = null;

				this.borderVisible = value;
			}
		}

		public void SetChild (IWidgetBackend child)
		{
			// Remove the old child before adding the new one
			// The child has to be removed from the viewport

			if (ScrollViewer.Content != null) {
				var port = (CustomScrollViewPort)ScrollViewer.Content;
				port.Children.Remove (port.Children[0]);
			}

			if (child == null)
				return;

			SetChildPlacement (child);
			ScrollAdjustmentBackend vbackend = null, hbackend = null;
			var widget = (WidgetBackend)child;

			if (widget.EventSink.SupportsCustomScrolling ()) {
				vscrollControl = vbackend = new ScrollAdjustmentBackend ();
				hscrollControl = hbackend = new ScrollAdjustmentBackend ();
			}
			ScrollViewer.Content = new CustomScrollViewPort (widget.NativeWidget, ScrollViewer, vbackend, hbackend);
			ScrollViewer.CanContentScroll = true;

			if (vbackend != null)
				widget.EventSink.SetScrollAdjustments (hbackend, vbackend);
		}
		
		public void SetChildSize (Size s)
		{

		}

		public Rectangle VisibleRect
		{
			get
			{
				return new Rectangle (	ScrollViewer.HorizontalOffset,
										ScrollViewer.VerticalOffset,
										ScrollViewer.ViewportWidth,
										ScrollViewer.ViewportHeight);
			}
		}

		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);

			if (eventId is ScrollViewEvent) {
				switch ((ScrollViewEvent)eventId) {
					case ScrollViewEvent.VisibleRectChanged:
						break;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent(eventId);

			if (eventId is ScrollViewEvent) {
				switch ((ScrollViewEvent)eventId) {
					case ScrollViewEvent.VisibleRectChanged:
						break;
				}
			}
		}

		protected override void Dispose (bool disposing)
		{
			if (ScrollViewer != null) {
				ScrollViewer.GotKeyboardFocus -= ScrollViewer_GotKeyboardFocus;
				ScrollViewer.LostKeyboardFocus -= ScrollViewer_LostKeyboardFocus;
			}

			base.Dispose (disposing);
		}

		protected ScrollViewer ScrollViewer
		{
			get { return (ScrollViewer) Widget; }
			set { Widget = value; }
		}

		private ScrollBarVisibility GetScrollVisibility (ScrollPolicy policy)
		{
			switch (policy) {
				case ScrollPolicy.Always:
					return ScrollBarVisibility.Visible;
				case ScrollPolicy.Automatic:
					return ScrollBarVisibility.Auto;
				case ScrollPolicy.Never:
					return ScrollBarVisibility.Disabled;

				default:
					throw new NotSupportedException();
			}
		}

		private ScrollPolicy GetScrollPolicy (ScrollBarVisibility visibility)
		{
			switch (visibility) {
				case ScrollBarVisibility.Auto:
					return ScrollPolicy.Automatic;
				case ScrollBarVisibility.Visible:
					return ScrollPolicy.Always;
				case ScrollBarVisibility.Disabled:
					return ScrollPolicy.Never;

				default:
					throw new NotSupportedException();
			}
		}
	}
}
