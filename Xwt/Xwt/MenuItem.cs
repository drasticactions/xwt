// 
// MenuItem.cs
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
using Xwt.Backends;
using System.ComponentModel;
using Xwt.Drawing;
using Xwt.Accessibility;

namespace Xwt
{
	[BackendType (typeof(IMenuItemBackend))]
	public class MenuItem: XwtComponent, ICellContainer
	{
		CellViewCollection cells;
		Menu subMenu;
		EventHandler clicked;
		Image image;
		
		protected class MenuItemBackendHost: BackendHost<MenuItem,IMenuItemBackend>, IMenuItemEventSink
		{
			protected override void OnBackendCreated ()
			{
				base.OnBackendCreated ();
				Backend.Initialize (this);
			}
			
			public void OnClicked ()
			{
				Parent.DoClick ();
			}
		}
		
		Accessible accessible;
		public Accessible Accessible {
			get {
				if (accessible == null) {
					accessible = new Accessible (this);
				}
				return accessible;
			}
		}

		protected override Xwt.Backends.BackendHost CreateBackendHost ()
		{
			return new MenuItemBackendHost ();
		}
		
		public MenuItem ()
		{
			if (!IsSeparator)
				UseMnemonic = true;
		}
		
		public MenuItem (Command command)
		{
			VerifyConstructorCall (this);
			LoadCommandProperties (command);
		}
		
		public MenuItem (string label)
		{
			VerifyConstructorCall (this);
			Label = label;
		}

		protected void LoadCommandProperties (Command command)
		{
			Label = command.Label;
			Image = command.Icon;
		}
		
		public IMenuItemBackend MenuItemBackend {
			get { return Backend; }
		}

		IMenuItemBackend Backend {
			get { return (IMenuItemBackend) base.BackendHost.Backend; }
		}

		bool IsSeparator {
			get { return this is SeparatorMenuItem; }
		}

		[DefaultValue ("")]
		public string Label {
			get { return Backend.Label; }
			set {
				if (IsSeparator)
					throw new NotSupportedException ();
				Backend.Label = value;
			}
		}

		string markup;
		/// <summary>
		/// Gets or sets the text with markup to display.
		/// </summary>
		/// <remarks>
		/// <see cref="Xwt.FormattedText"/> for supported formatting options.</remarks>
		[DefaultValue ("")]
		public string Markup {
			get { return markup; }
			set {
				markup = value;
				var t = FormattedText.FromMarkup (markup);
				Backend.SetFormattedText (t);
			}
		}

		/// <summary>
		/// Gets or sets the tooltip text.
		/// </summary>
		/// <value>The tooltip text.</value>
		[DefaultValue("")]
		public string TooltipText
		{
			get { return Backend.TooltipText ?? ""; }
			set
			{
				if (IsSeparator)
					throw new NotSupportedException();
				Backend.TooltipText = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Xwt.Button"/> uses a mnemonic.
		/// </summary>
		/// <value><c>true</c> if it uses a mnemonic; otherwise, <c>false</c>.</value>
		/// <remarks>
		/// When set to true, the character after the first underscore character in the Label property value is
		/// interpreted as the mnemonic for that Label.
		/// </remarks>
		[DefaultValue(true)]
		public bool UseMnemonic { 
			get { return Backend.UseMnemonic; }
			set {
				if (IsSeparator)
					throw new NotSupportedException ();
				Backend.UseMnemonic = value;
			}
		}
		
		[DefaultValue (true)]
		public bool Sensitive {
			get { return Backend.Sensitive; }
			set { Backend.Sensitive = value; }
		}
		
		[DefaultValue (true)]
		public bool Visible {
			get { return Backend.Visible; }
			set { Backend.Visible = value; }
		}

		public bool IsSubMenuOpen {
			get { return Backend.IsSubMenuOpen; }
			set { Backend.IsSubMenuOpen = value; }
		}
		

		public KeyShortcut Shortcut {
			get { return Backend.Shortcut; }
			set { Backend.Shortcut = value; }
		}

		[DefaultValue("")]
		public string ToolTip {
			get { return Backend.ToolTip; }
			set {
				if(IsSeparator) {
					throw new NotSupportedException();
				}
				Backend.ToolTip = value;
			}
		}

		public Image Image {
			get { return image; }
			set {
				if (IsSeparator)
					throw new NotSupportedException ();
				image = value; 
				if (!IsSeparator)
					Backend.SetImage (image != null ? image.GetImageDescription (BackendHost.ToolkitEngine) : ImageDescription.Null);
			}
		}
		
		public void Show ()
		{
			Visible = true;
		}
		
		public void Hide ()
		{
			Visible = false;
		}
		
		public CellViewCollection Cells {
			get {
				if (cells == null)
					cells = new CellViewCollection (this);
				return cells;
			}
		}
		
		public Menu SubMenu {
			get { return subMenu; }
			set {
				if (IsSeparator)
					throw new NotSupportedException ();
				Backend.SetSubmenu ((IMenuBackend)BackendHost.ToolkitEngine.GetSafeBackend (value));
				subMenu = value;
			}
		}
		
		public void NotifyCellChanged ()
		{
			throw new NotImplementedException ();
		}
		
		internal virtual void DoClick ()
		{
			OnClicked (EventArgs.Empty);
		}
		
		[MappedEvent(MenuItemEvent.Clicked)]
		protected virtual void OnClicked (EventArgs e)
		{
			if (clicked != null)
				clicked (this, e);
		}
		
		public event EventHandler Clicked {
			add {
				base.BackendHost.OnBeforeEventAdd (MenuItemEvent.Clicked, clicked);
				clicked += value;
			}
			remove {
				clicked -= value;
				base.BackendHost.OnAfterEventRemove (MenuItemEvent.Clicked, clicked);
			}
		}

		protected override void Dispose (bool release_all)
		{
			if (release_all) {
				Backend.Dispose ();
			}
			base.Dispose (release_all);
		}
	}
	
	public enum MenuItemType
	{
		Normal,
		CheckBox,
		RadioButton
	}

	public class KeyShortcut {
		public KeyboardKey Key { get; set; }
		public KeyboardKeyModifiers Modifiers { get; set; }

		public KeyShortcut(KeyboardKey key, KeyboardKeyModifiers modifiers) {
			this.Key = key;
			this.Modifiers = modifiers;
		}
	}
}

