// 
// CheckBox.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
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
using System.ComponentModel;
using Xwt.Backends;

namespace Xwt
{
	[BackendType (typeof(ICheckBoxBackend))]
	public class CheckBox: Widget
	{
		Widget content;
		EventHandler clicked;
		EventHandler toggled;
		string label = "";
		bool useMnemonic = true;
		
		protected new class WidgetBackendHost: Widget.WidgetBackendHost, ICheckBoxEventSink
		{
			public void OnClicked ()
			{
				((CheckBox)Parent).OnClicked (EventArgs.Empty);
			}
			public void OnToggled ()
			{
				((CheckBox)Parent).OnToggled (EventArgs.Empty);
			}
		}
		
		public CheckBox ()
		{
		}
		
		public CheckBox (string label)
		{
			VerifyConstructorCall (this);
			Label = label;
		}
		
		protected override BackendHost CreateBackendHost ()
		{
			return new WidgetBackendHost ();
		}
		
		ICheckBoxBackend Backend {
			get { return (ICheckBoxBackend) BackendHost.Backend; }
		}
		
		[DefaultValue ("")]
		public string Label {
			get { return label; }
			set {
				label = value;
				Backend.SetContent (label, useMnemonic);
				OnPreferredSizeChanged ();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Xwt.CheckBox"/> uses a mnemonic.
		/// </summary>
		/// <value><c>true</c> if it uses a mnemonic; otherwise, <c>false</c>.</value>
		/// <remarks>
		/// When set to true, the character after the first underscore character in the Label property value is
		/// interpreted as the mnemonic for that Label.
		/// </remarks>
		[DefaultValue (true)]
		public bool UseMnemonic {
			get { return useMnemonic; }
			set {
				if (useMnemonic == value)
					return;
				Backend.SetContent (label, value);
				useMnemonic = value;
			}
		}

		[DefaultValue (null)]
		public new Widget Content {
			get { return content; }
			set {
				if (content != null)
					UnregisterChild (content);
				content = value;
				if (content != null)
					RegisterChild (content);
				Backend.SetContent ((IWidgetBackend)GetBackend (content));
				OnPreferredSizeChanged ();
			}
		}
		
		[DefaultValue (false)]
		public bool Active {
			get { return State == CheckBoxState.On;}
			set { State = value.ToCheckBoxState (); }
		}
		
		[DefaultValue (false)]
		public CheckBoxState State {
			get { return Backend.State; }
			set {
				if (!value.IsValid ())
					throw new ArgumentOutOfRangeException (nameof(value), "Invalid check box state value");
				Backend.State = value;
			}
		}
		
		[DefaultValue (false)]
		public bool AllowMixed {
			get { return Backend.AllowMixed; }
			set { Backend.AllowMixed = value; }
		}
		
		[MappedEvent(CheckBoxEvent.Clicked)]
		protected virtual void OnClicked (EventArgs e)
		{
			if (clicked != null)
				clicked (this, e);
		}
		
		[MappedEvent(CheckBoxEvent.Toggled)]
		protected virtual void OnToggled (EventArgs e)
		{
			if (toggled != null)
				toggled (this, e);
		}
		
		public event EventHandler Clicked {
			add {
				BackendHost.OnBeforeEventAdd (CheckBoxEvent.Clicked, clicked);
				clicked += value;
			}
			remove {
				clicked -= value;
				BackendHost.OnAfterEventRemove (CheckBoxEvent.Clicked, clicked);
			}
		}
		
		public event EventHandler Toggled {
			add {
				BackendHost.OnBeforeEventAdd (CheckBoxEvent.Toggled, toggled);
				toggled += value;
			}
			remove {
				toggled -= value;
				BackendHost.OnAfterEventRemove (CheckBoxEvent.Toggled, toggled);
			}
		}
	}
}

