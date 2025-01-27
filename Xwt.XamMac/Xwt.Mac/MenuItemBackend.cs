// 
// MenuItemBackend.cs
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

using System;
using System.Collections.Generic;
using AppKit;
using Xwt.Backends;

namespace Xwt.Mac
{
	public class MenuItemBackend : IMenuItemBackend
	{
		NSMenuItem item;
		IMenuItemEventSink eventSink;
		List<MenuItemEvent> enabledEvents;
		ApplicationContext context;
		string label;
		bool useMnemonic;

		private NSEventModifierMask GetModifierMask(KeyShortcut accel) {
			NSEventModifierMask mask = default(NSEventModifierMask);

			if(accel.Modifiers.HasFlag(KeyboardKeyModifiers.Command)) {
				mask |= NSEventModifierMask.CommandKeyMask;
			}

			if(accel.Modifiers.HasFlag(KeyboardKeyModifiers.Shift)) {
				mask |= NSEventModifierMask.ShiftKeyMask;
			}

			if(accel.Modifiers.HasFlag(KeyboardKeyModifiers.Alt)) {
				mask |= NSEventModifierMask.AlternateKeyMask;
			}

			if(accel.Modifiers.HasFlag(KeyboardKeyModifiers.Control)) {
				mask |= NSEventModifierMask.ControlKeyMask;
			}

			return mask;
		}

		private KeyShortcut shortcut;
		public KeyShortcut Shortcut {
			get {
				return shortcut;
			}
			set {
				shortcut = value;
				if(value.Modifiers.HasFlag(KeyboardKeyModifiers.Shift)) {
					item.KeyEquivalent = value.Key.MacMenuCharacter.ToString();
				} else {
					item.KeyEquivalent = value.Key.MacMenuCharacter.ToString().ToLower();
				}
				item.KeyEquivalentModifierMask = GetModifierMask(value);
			}
		}

		public MenuItemBackend (): this (new NSMenuItem ())
		{
		}

		public MenuItemBackend(NSMenuItem item)
		{
			this.item = item;
		}

		public NSMenuItem Item
		{
			get { return item; }
		}

		public void Initialize(IMenuItemEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		public void SetSubmenu(IMenuBackend menu)
		{
			if (menu == null)
				item.Submenu = null;
			else
				item.Submenu = ((MenuBackend)menu);
		}

		public string Label
		{
			get
			{
				return label;
			}
			set
			{
				if (item.AttributedTitle != null) // once set, AttributedTitle can not be removed, so let's just use it
					item.AttributedTitle = new Foundation.NSAttributedString (value.RemoveMnemonic());
				else
					item.Title = UseMnemonic ? value.RemoveMnemonic() : value;
				label = value;
			}
		}

		public string TooltipText
		{
			get
			{
				return item.ToolTip;
			}
			set
			{
				item.ToolTip = value;
			}
		}

		public bool UseMnemonic
		{
			get
			{
				return useMnemonic;
			}
			set
			{
				useMnemonic = value;
				Label = label ?? string.Empty;
			}
		}

		public void SetImage(ImageDescription image)
		{
			item.Image = image.ToNSImage();
		}

		public bool Visible
		{
			get
			{
				return !item.Hidden;
			}
			set
			{
				item.Hidden = !value;
			}
		}
		
		public bool IsSubMenuOpen {
			get {
				// sorry - can't do this on macOS - listen to the opened/closed events instead (they don't work properly on Windows, but this function does, which is why it exists)
				throw new NotImplementedException();
			}
			set {
				throw new NotImplementedException();
			}
		}

		public bool Sensitive
		{
			get
			{
				return item.Enabled;
			}
			set
			{
				item.Enabled = value;
			}
		}

		public bool Checked
		{
			get
			{
				return item.State == NSCellStateValue.On;
			}
			set
			{
				if (value)
					item.State = NSCellStateValue.On;
				else
					item.State = NSCellStateValue.Off;
			}
		}

		public void SetFormattedText (FormattedText text)
		{
			item.AttributedTitle = text.ToAttributedString ();
		}

		public string ToolTip {
			get {
				return item.ToolTip;
			}
			set {
				item.ToolTip = value;
			}
		}

		#region IBackend implementation
		public void InitializeBackend(object frontend, ApplicationContext context)
		{
			this.context = context;
		}

		public void EnableEvent(object eventId)
		{
			if (eventId is MenuItemEvent)
			{
				if (enabledEvents == null)
					enabledEvents = new List<MenuItemEvent>();
				enabledEvents.Add((MenuItemEvent)eventId);
				if ((MenuItemEvent)eventId == MenuItemEvent.Clicked)
					item.Activated += HandleItemActivated;
			}
		}

		public void DisableEvent(object eventId)
		{
			if (eventId is MenuItemEvent)
			{
				enabledEvents.Remove((MenuItemEvent)eventId);
				if ((MenuItemEvent)eventId == MenuItemEvent.Clicked)
					item.Activated -= HandleItemActivated;
			}
		}
		#endregion

		void HandleItemActivated(object sender, EventArgs e)
		{
			context.InvokeUserCode(delegate
			{
				eventSink.OnClicked();
			});
		}

		public void Dispose ()
		{
			// Nothing to do here.
		}
	}
}

