// 
// MenuBackend.cs
//  
// Author:
//       Carlos Alberto Cortez <calberto.cortez@gmail.com>
//       Luís Reis <luiscubal@gmail.com>
//       Eric Maupin <ermau@xamarin.com>
// 
// Copyright (c) 2011 Carlos Alberto Cortez
// Copyright (c) 2012 Luís Reis
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

using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xwt.Backends;

namespace Xwt.WPFBackend
{
	public class MenuBackend : Backend, IMenuBackend
	{
		List<MenuItemBackend> items;
		FontData customFont;
		UIElement dummyAccessibiltyUIElement;
		IMenuEventSink eventSink;

		public void Initialize(IMenuEventSink eventSink) {
			this.eventSink = eventSink;
		}

		public override void InitializeBackend (object frontend, ApplicationContext context)
		{
			base.InitializeBackend (frontend, context);
			items = new List<MenuItemBackend> ();
			dummyAccessibiltyUIElement = new UIElement ();
		}

		public IList<MenuItemBackend> Items {
			get {
				return items;
			}
		}

		public MenuItemBackend ParentItem {
			get;
			set;
		}

		public WindowBackend ParentWindow {
			get;
			set;
		}

		public ContextMenu NativeMenu => menu;

		public UIElement DummyAccessibilityUIElement => dummyAccessibiltyUIElement;

		new Menu Frontend {
			get { return (Menu)base.frontend; }
		}

		public virtual object Font {
			get {
				if (customFont == null)
					return FontData.FromControl (Items.Count > 0 ? Items[0].MenuItem : new System.Windows.Controls.MenuItem());
				return customFont;
			}
			set {
				customFont = (FontData)value;
				foreach (var item in Items)
					item.SetFont (customFont);
			}
		}

		public void InsertItem (int index, IMenuItemBackend item)
		{
			var itemBackend = (MenuItemBackend)item;
			if (customFont != null)
				itemBackend.SetFont(customFont);
			items.Insert (index, itemBackend);
			if (ParentItem != null && ParentItem.MenuItem != null)
				ParentItem.MenuItem.Items.Insert (index, itemBackend.Item);
			else if (ParentWindow != null)
				ParentWindow.mainMenu.Items.Insert (index, itemBackend.Item);
			else if (this.menu != null)
				this.menu.Items.Insert (index, itemBackend.Item);
		}

		public void RemoveItem (IMenuItemBackend item)
		{
			var itemBackend = (MenuItemBackend)item;
			items.Remove (itemBackend);
			if (ParentItem != null)
				ParentItem.MenuItem.Items.Remove (itemBackend.Item);
			else if (ParentWindow != null)
				ParentWindow.mainMenu.Items.Remove (itemBackend.Item);
			else if (this.menu != null)
				this.menu.Items.Remove (itemBackend.Item);
		}

		public void RemoveFromParentItem ()
		{
			if (ParentItem == null)
				return;

			ParentItem.MenuItem.Items.Clear ();
			ParentItem = null;
		}

		public void Popup (IWidgetBackend widget)
		{
			var menu = CreateContextMenu ();
			var target = widget.NativeWidget as UIElement;
			if(target == null)
				throw new System.ArgumentException("Widget belongs to an unsupported Toolkit", nameof(widget));
			menu.PlacementTarget = target;
			menu.Placement = PlacementMode.MousePoint;
			menu.IsOpen = true;
		}

		public void Popup (IWidgetBackend widget, double x, double y)
		{
			var menu = CreateContextMenu ();
			var target = widget.NativeWidget as UIElement;
			if (target == null)
				throw new System.ArgumentException ("Widget belongs to an unsupported Toolkit", nameof (widget));
			menu.PlacementTarget = target;
			menu.Placement = PlacementMode.Relative;
			menu.HorizontalOffset = x;
			menu.VerticalOffset = y;
			menu.IsOpen = true;
		}

		private ContextMenu menu;
		internal ContextMenu CreateContextMenu()
		{
			if (this.menu == null) {
				this.menu = new ContextMenu ();

				foreach (var item in Items)
					this.menu.Items.Add (item.Item);

				var accessibleBackend = (AccessibleBackend)Toolkit.GetBackend (Frontend.Accessible);
				if (accessibleBackend != null)
					accessibleBackend.InitAutomationProperties (menu);

				menu.Opened += (object sender, RoutedEventArgs e) => {
					this.Context.InvokeUserCode(eventSink.OnOpening);
				};

				menu.Closed += (object sender, RoutedEventArgs e) => {
					this.Context.InvokeUserCode(eventSink.OnClosed);
				};
			}

			return menu;
		}

		public ContextMenu ContextMenu {
			get {
				return menu;
			}
		}

		public override void EnableEvent(object eventId) {
			if(eventId is MenuEvent) {
				switch((MenuEvent)eventId) {
				case MenuEvent.Opening:
					if(this.ParentItem != null) {
						this.ParentItem.MenuItem.SubmenuOpened += SubmenuOpenedHandler;
					}
					break;

				case MenuEvent.Closed:
					if(this.ParentItem != null) {
						this.ParentItem.MenuItem.SubmenuClosed += SubmenuClosedHandler;
					}
					break;
				}
			}
		}

		public override void DisableEvent(object eventId) {
			if(eventId is MenuEvent) {
				switch((MenuEvent)eventId) {
				case MenuEvent.Opening:
					if(this.ParentItem != null) {
						this.ParentItem.MenuItem.SubmenuOpened -= SubmenuOpenedHandler;
					}
					break;

				case MenuEvent.Closed:
					if(this.ParentItem != null) {
						this.ParentItem.MenuItem.SubmenuClosed -= SubmenuClosedHandler;
					}
					break;
				}
			}
		}

		private void SubmenuOpenedHandler(object sender, RoutedEventArgs e)
		{
			if((e.Source as System.Windows.Controls.MenuItem) == this.ParentItem.MenuItem)
			{
				Context.InvokeUserCode(eventSink.OnOpening);    
			}
		}

		private void SubmenuClosedHandler(object sender, RoutedEventArgs e) {
			if((e.Source as System.Windows.Controls.MenuItem) == this.ParentItem.MenuItem) {
				Context.InvokeUserCode(eventSink.OnClosed);
			}
		}
	}
}
