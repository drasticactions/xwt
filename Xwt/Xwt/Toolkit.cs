//
// ToolkitEngine.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc
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
using Xwt.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Xwt
{
	public sealed class Toolkit: IFrontend
	{
		static Toolkit currentEngine;
		static Toolkit nativeEngine;
		static Dictionary<Type, Toolkit> toolkits = new Dictionary<Type, Toolkit> ();

		ToolkitEngineBackend backend;
		ApplicationContext context;
		XwtTaskScheduler scheduler;
		ToolkitType toolkitType;
		ToolkitDefaults defaults;
		XwtSynchronizationContext synchronizationContext;

		int inUserCode;
		Queue<Action> exitActions = new Queue<Action> ();
		bool exitCallbackRegistered;

		static KnownBackend[] knownBackends = new [] {
			new KnownBackend { Type = ToolkitType.Gtk3, TypeName = "Xwt.GtkBackend.GtkEngine, Xwt.Gtk3" },
			new KnownBackend { Type = ToolkitType.Gtk, TypeName = "Xwt.GtkBackend.GtkEngine, Xwt.Gtk" },
			new KnownBackend { Type = ToolkitType.XamMac, TypeName = "Xwt.Mac.MacEngine, Xwt.XamMac" },
			new KnownBackend { Type = ToolkitType.Cocoa, TypeName = "Xwt.Mac.MacEngine, Xwt.XamMac" },
			new KnownBackend { Type = ToolkitType.Wpf, TypeName = "Xwt.WPFBackend.WPFEngine, Xwt.WPF" },
		};

		class KnownBackend
		{
			public ToolkitType Type { get; set; }
			public string TypeName { get; set; }

			public string FullTypeName {
				get {
					return TypeName + ", Version=" + typeof(Application).Assembly.GetName ().Version;
				}
			}
		}

		Dictionary<string,Image> stockIcons = new Dictionary<string, Image> ();

		/// <summary>
		/// Gets the current toolkit engine.
		/// </summary>
		/// <value>The engine currently used by Xwt.</value>
		public static Toolkit CurrentEngine {
			get { return currentEngine; }
		}

		/// <summary>
		/// Gets the native platform toolkit engine.
		/// </summary>
		/// <value>The native engine.</value>
		public static Toolkit NativeEngine {
			get {
				if (nativeEngine == null) {
					switch (Desktop.DesktopType) {
						case DesktopType.Linux:
							// don't mix Gtk2 and Gtk3
							if (CurrentEngine != null && (CurrentEngine.Type == ToolkitType.Gtk || CurrentEngine.Type == ToolkitType.Gtk3))
								nativeEngine = CurrentEngine;
							else if (!TryLoad (ToolkitType.Gtk3, out nativeEngine))
								TryLoad (ToolkitType.Gtk, out nativeEngine);
							break;
						case DesktopType.Windows:
							TryLoad (ToolkitType.Wpf, out nativeEngine);
							break;
						case DesktopType.Mac:
							TryLoad (ToolkitType.XamMac, out nativeEngine);
							break;
					}
				}
				if (nativeEngine == null)
					nativeEngine = CurrentEngine;
				return nativeEngine;
			}
		}

		/// <summary>
		/// Gets all loaded toolkits.
		/// </summary>
		/// <value>The loaded toolkits.</value>
		public static IEnumerable<Toolkit> LoadedToolkits {
			get { return toolkits.Values; }
		}

		/// <summary>
		/// Gets the application context.
		/// </summary>
		/// <value>The application context.</value>
		internal ApplicationContext Context {
			get { return context; }
		}

		/// <summary>
		/// Gets the toolkit backend.
		/// </summary>
		/// <value>The toolkit backend.</value>
		internal ToolkitEngineBackend Backend {
			get { return backend; }
		}

		/// <summary>
		/// Gets the toolkit task scheduler.
		/// </summary>
		/// <value>The toolkit specific task scheduler.</value>
		/// <remarks>
		/// The Xwt task scheduler marshals every Task to the Xwt GUI thread without concurrency.
		/// </remarks>
		internal XwtTaskScheduler Scheduler {
			get { return scheduler; }
		}

		object IFrontend.Backend {
			get { return backend; }
		}
		Toolkit IFrontend.ToolkitEngine {
			get { return this; }
		}

		private Toolkit ()
		{
			synchronizationContext = new XwtSynchronizationContext (this);
			context = new ApplicationContext (this);
			scheduler = new XwtTaskScheduler (this);
		}

		/// <summary>
		/// Gets a synchronization context for this toolkit.
		/// </summary>
		/// <value>The synchronization context.</value>
		public XwtSynchronizationContext SynchronizationContext {
			get { return synchronizationContext; }
		}

		/// <summary>
		/// Gets or sets the type of the toolkit.
		/// </summary>
		/// <value>The toolkit type.</value>
		public ToolkitType Type {
			get { return toolkitType; }
			internal set { toolkitType = value; }
		}

		/// <summary>
		/// Disposes all loaded toolkits.
		/// </summary>
		internal static void DisposeAll ()
		{
			foreach (var t in toolkits.Values)
				t.Backend.Dispose ();
		}

		/// <summary>
		/// Load toolkit identified by its full type name.
		/// </summary>
		/// <param name="fullTypeName">The <see cref="Type.FullName"/> of the toolkit type.</param>
		public static Toolkit Load (string fullTypeName)
		{
			return Load (fullTypeName, true, true);
		}

		/// <summary>
		/// Load toolkit identified by its full type name.
		/// </summary>
		/// <param name="fullTypeName">The <see cref="Type.FullName"/> of the toolkit type.</param>
		/// <param name="isGuest">If set to <c>true</c> the toolkit is loaded as guest of another toolkit.</param>
        /// <param name="initializeToolkit">If set to <c>true</c> the parent toolkit is initialized.</param>
		internal static Toolkit Load (string fullTypeName, bool isGuest, bool initializeToolkit)
		{
			Toolkit t = new Toolkit ();

			if (!string.IsNullOrEmpty (fullTypeName)) {
				t.LoadBackend (fullTypeName, isGuest, initializeToolkit, true);
				var bk = knownBackends.FirstOrDefault (tk => fullTypeName.StartsWith (tk.TypeName));
				if (bk != null)
					t.Type = bk.Type;
				return t;
			}

			foreach (var bk in knownBackends) {
				if (t.LoadBackend (bk.FullTypeName, isGuest, initializeToolkit, false)) {
					t.Type = bk.Type;
					return t;
				}
			}

			throw new InvalidOperationException ("Xwt engine not found");
		}

		/// <summary>
		/// Load a toolkit of a specified type.
		/// </summary>
		/// <param name="type">The toolkit type.</param>
		public static Toolkit Load (ToolkitType type)
		{
			var et = toolkits.Values.FirstOrDefault (tk => tk.toolkitType == type);
			if (et != null)
				return et;

			Toolkit t = new Toolkit ();
			t.toolkitType = type;
			t.LoadBackend (GetBackendType (type), true, true, true);
			return t;
		}

		/// <summary>
		/// Tries to load a toolkit
		/// </summary>
		/// <returns><c>true</c>, the toolkit has been loaded, <c>false</c> otherwise.</returns>
		/// <param name="type">Toolkit type</param>
		/// <param name="toolkit">The loaded toolkit</param>
		public static bool TryLoad (ToolkitType type, out Toolkit toolkit)
		{
			var et = toolkits.Values.FirstOrDefault (tk => tk.toolkitType == type);
			if (et != null) {
				toolkit = et;
				return true;
			}

			Toolkit t = new Toolkit ();
			t.toolkitType = type;
			if (t.LoadBackend (GetBackendType (type), true, true, false)) {
				toolkit = t;
				return true;
			}
			toolkit = null;
			return false;
		}

		/// <summary>
		/// Gets the <see cref="Type.FullName"/> of the toolkit identified by toolkit type.
		/// </summary>
		/// <returns>The toolkit type name.</returns>
		/// <param name="type">The toolkit type.</param>
		internal static string GetBackendType (ToolkitType type)
		{
			var t = knownBackends.FirstOrDefault (tk => tk.Type == type);
			if (t != null)
				return t.FullTypeName;

			throw new ArgumentException ("Invalid toolkit type");
		}

		bool LoadBackend (string type, bool isGuest, bool initializeToolkit, bool throwIfFails)
		{
			int i = type.IndexOf (',');
			string assembly = type.Substring (i+1).Trim ();
			type = type.Substring (0, i).Trim ();
			try {
				Assembly asm = null;
				try {
					asm = Assembly.Load (assembly);
				} catch (System.IO.FileNotFoundException) {
					// This will happen when assemblies are merged for deployment
					asm = Assembly.GetExecutingAssembly();
				}
				if (asm != null) {
					Type t = asm.GetType (type);
					if (t != null) {
						backend = (ToolkitEngineBackend) Activator.CreateInstance (t);
						Initialize (isGuest, initializeToolkit);
						return true;
					}
				}
			}
			catch (Exception ex) {
				if (throwIfFails)
					throw new Exception ("Toolkit could not be loaded", ex);
			}
			if (throwIfFails)
				throw new Exception ("Toolkit could not be loaded");
			return false;
		}

		void Initialize (bool isGuest, bool initializeToolkit)
		{
			toolkits[Backend.GetType ()] = this;
			backend.Initialize (this, isGuest, initializeToolkit);
			ContextBackendHandler = Backend.CreateBackend<ContextBackendHandler> ();
			GradientBackendHandler = Backend.CreateBackend<GradientBackendHandler> ();
			TextLayoutBackendHandler = Backend.CreateBackend<TextLayoutBackendHandler> ();
			FontBackendHandler = Backend.CreateBackend<FontBackendHandler> ();
			ClipboardBackend = Backend.CreateBackend<ClipboardBackend> ();
			ImageBuilderBackendHandler = Backend.CreateBackend<ImageBuilderBackendHandler> ();
			ImagePatternBackendHandler = Backend.CreateBackend<ImagePatternBackendHandler> ();
			ImageBackendHandler = Backend.CreateBackend<ImageBackendHandler> ();
			DrawingPathBackendHandler = Backend.CreateBackend<DrawingPathBackendHandler> ();
			DesktopBackend = Backend.CreateBackend<DesktopBackend> ();
			VectorImageRecorderContextHandler = new VectorImageRecorderContextHandler (this);
			KeyboardHandler = Backend.CreateBackend<KeyboardHandler> ();
		}

		/// <summary>
		/// Gets the toolkit backend from a loaded toolkit.
		/// </summary>
		/// <returns>The toolkit backend, or <c>null</c> if the toolkit is not loaded.</returns>
		/// <param name="type">The Type of the loaded toolkit.</param>
		internal static ToolkitEngineBackend GetToolkitBackend (Type type)
		{
			Toolkit t;
			if (toolkits.TryGetValue (type, out t))
				return t.backend;
			else
				return null;
		}

		/// <summary>
		/// Set the toolkit as the active toolkit used by Xwt.
		/// </summary>
		internal void SetActive ()
		{
			currentEngine = this;
		}

		/// <summary>
		/// Gets the defaults for the current toolkit.
		/// </summary>
		/// <value>The toolkit defaults.</value>
		public ToolkitDefaults Defaults {
			get {
				if (defaults == null)
					defaults = new ToolkitDefaults ();
				return defaults;
			}
		}

		/// <summary>
		/// Gets a reference to the native widget wrapped by an Xwt widget
		/// </summary>
		/// <returns>The native widget currently used by Xwt for the specific widget.</returns>
		/// <param name="w">The Xwt widget.</param>
		public object GetNativeWidget (Widget w)
		{
			ValidateObject (w);
			w.SetExtractedAsNative ();
			return backend.GetNativeWidget (w);
		}

		/// <summary>
		/// Gets a reference to the native window wrapped by an Xwt window
		/// </summary>
		/// <returns>The native window currently used by Xwt for the specific window, or null.</returns>
		/// <param name="w">The Xwt window.</param>
		/// <remarks>
		/// If the window backend belongs to a different toolkit and the current toolkit is the
		/// native toolkit for the current platform, GetNativeWindow will return the underlying
		/// native window, or null if the operation is not supported for the current toolkit.
		/// </remarks>
		public object GetNativeWindow (WindowFrame w)
		{
			return backend.GetNativeWindow (w);
		}

		/// <summary>
		/// Gets a reference to the native platform window used by the specified backend.
		/// </summary>
		/// <returns>The native window currently used by Xwt for the specific window, or null.</returns>
		/// <param name="w">The Xwt window.</param>
		/// <remarks>
		/// If the window backend belongs to a different toolkit and the current toolkit is the
		/// native toolkit for the current platform, GetNativeWindow will return the underlying
		/// native window, or null if the operation is not supported for the current toolkit.
		/// </remarks>
		public object GetNativeWindow (IWindowFrameBackend w)
		{
			return backend.GetNativeWindow (w);
		}

		/// <summary>
		/// Gets a reference to the image object wrapped by an XWT Image
		/// </summary>
		/// <returns>The native image object used by Xwt for the specific image.</returns>
		/// <param name="image">The native Image object.</param>
		public object GetNativeImage (Image image)
		{
			ValidateObject (image);
			return backend.GetNativeImage (image);
		}
		
		public object GetNativeWindow (Window window) {
			return window.WindowBackend;
		}

		/// <summary>
		/// Creates a native toolkit object.
		/// </summary>
		/// <returns>A new native toolkit object.</returns>
		/// <typeparam name="T">The type of the object to create.</typeparam>
		public T CreateObject<T> () where T:new()
		{
			var oldEngine = currentEngine;
			try {
				currentEngine = this;
				return new T ();
			} finally {
				currentEngine = oldEngine;
			}
		}

		/// <summary>
		/// Switches the current context to the context of this toolkit
		/// </summary>
		ToolkitContext SwitchContext ()
		{
			var current = System.Threading.SynchronizationContext.Current;

			// Store the current engine and the current context (which is not necessarily the context of the engine)
			var currentContext = new ToolkitContext {
				SynchronizationContext = current,
				Engine = currentEngine
			};

			currentEngine = this;
			if ((current as XwtSynchronizationContext)?.TargetToolkit != this)
				System.Threading.SynchronizationContext.SetSynchronizationContext (SynchronizationContext);
			return currentContext;
		}

		struct ToolkitContext
		{
			public SynchronizationContext SynchronizationContext;
			public Toolkit Engine;

			public void Restore ()
			{
				Toolkit.currentEngine = Engine;
				System.Threading.SynchronizationContext.SetSynchronizationContext (SynchronizationContext);
			}
		}

		/// <summary>
		/// Invokes the specified action using this toolkit.
		/// </summary>
		/// <param name="a">The action to invoke in the context of this toolkit.</param>
		/// <remarks>
		/// Invoke allows dynamic toolkit switching. It will set <see cref="CurrentEngine"/> to this toolkit and reset
		/// it back to its original value after the action has been executed.
		/// 
		/// Invoke must be executed on the UI thread. The action will not be synchronized with the main UI thread automatically.
		/// </remarks>
		/// <returns><c>true</c> if the action has been executed sucessfully; otherwise, <c>false</c>.</returns>
		public bool Invoke (Action a)
		{
			ToolkitContext currentContext = SwitchContext ();
			try {
				EnterUserCode ();
				a ();
				ExitUserCode (null);
				return true;
			} catch (Exception ex) {
				ExitUserCode (ex);
				return false;
			} finally {
				currentContext.Restore ();
			}
		}

		public T Invoke<T> (Func<T> func)
		{
			ToolkitContext currentContext = SwitchContext ();
			try {
				EnterUserCode ();
				var res = func ();
				ExitUserCode (null);
				return res;
			} catch (Exception ex) {
				ExitUserCode (ex);
				return default (T);
			} finally {
				currentContext.Restore ();
			}
		}

		internal void InvokeAndThrow (Action a)
		{
			var currentContext = SwitchContext ();
			try {
				EnterUserCode ();
				a ();
			} finally {
				ExitUserCode (null);
				currentContext.Restore ();
			}
		}

		internal T InvokeAndThrow<T> (Func<T> func)
		{
			var currentContext = SwitchContext ();
			try {
				currentEngine = this;
				EnterUserCode ();
				return func ();
			} finally {
				ExitUserCode (null);
				currentContext.Restore ();
			}
		}

		/// <summary>
		/// Invokes an action after the user code has been processed.
		/// </summary>
		/// <param name="a">The action to invoke after processing user code.</param>
		public void InvokePlatformCode (Action a)
		{
			int prevCount = inUserCode;
			inUserCode = 1;
			ExitUserCode (null);
			var currentContext = Application.MainLoop.Engine.SwitchContext ();

			try {
				a();
			} finally {
				inUserCode = prevCount;
				currentContext.Restore ();
			}
		}
		
		/// <summary>
		/// Enters the user code.
		/// </summary>
		/// <remarks>EnterUserCode must be called before executing any user code.</remarks>
		internal void EnterUserCode ()
		{
			inUserCode++;
		}
		
		/// <summary>
		/// Exits the user code.
		/// </summary>
		/// <param name="error">Exception thrown during user code execution, or <c>null</c></param>
		internal void ExitUserCode (Exception error)
		{
			if (error != null) {
				Invoke (delegate {
					Application.NotifyException (error);
				});
			}
			if (inUserCode == 1 && !exitCallbackRegistered) {
				while (exitActions.Count > 0) {
					try {
						exitActions.Dequeue ()();
					} catch (Exception ex) {
						Invoke (delegate {
							Application.NotifyException (ex);
						});
					}
				}
			}
			inUserCode--;
		}

		void DispatchExitActions ()
		{
			// This pair of calls will flush the exit action queue
			exitCallbackRegistered = false;
			EnterUserCode ();
			ExitUserCode (null);
		}
		
		/// <summary>
		/// Adds the action to the exit action queue.
		/// </summary>
		/// <param name="a">The action to invoke after processing user code.</param>
		internal void QueueExitAction (Action a)
		{
			exitActions.Enqueue (a);

			if (inUserCode == 0) {
				// Not in an XWT handler. This may happen when embedding XWT in another toolkit and
				// XWT widgets are manipulated from event handlers of the native toolkit which
				// are not invoked using ApplicationContext.InvokeUserCode.
				if (!exitCallbackRegistered) {
					exitCallbackRegistered = true;
					// Try to use a native method of queuing exit actions
					Toolkit.CurrentEngine.Backend.InvokeBeforeMainLoop (DispatchExitActions);
				}
			}
		}
		
		/// <summary>
		/// Gets a value indicating whether the GUI Thread is currently executing user code.
		/// </summary>
		/// <value><c>true</c> if in user code; otherwise, <c>false</c>.</value>
		public bool InUserCode {
			get { return inUserCode > 0; }
		}

		/// <summary>
		/// Wraps a native window into an Xwt window object.
		/// </summary>
		/// <returns>An Xwt window with the specified native window backend.</returns>
		/// <param name="nativeWindow">The native window.</param>
		public WindowFrame WrapWindow (object nativeWindow)
		{
			if (nativeWindow == null)
				return null;
			return new NativeWindowFrame (backend.GetBackendForWindow (nativeWindow));
		}

		/// <summary>
		/// Wraps a native widget into an Xwt widget object.
		/// </summary>
		/// <returns>An Xwt widget with the specified native widget backend.</returns>
		/// <param name="nativeWidget">The native widget.</param>
		public Widget WrapWidget (object nativeWidget, NativeWidgetSizing preferredSizing = NativeWidgetSizing.External, bool reparent = true)
		{
			var externalWidget = nativeWidget as Widget;
			if (externalWidget != null) {
				if (externalWidget.Surface.ToolkitEngine == this)
					return externalWidget;
				nativeWidget = externalWidget.Surface.ToolkitEngine.GetNativeWidget (externalWidget);
			}
			var embedded = CreateObject<EmbeddedNativeWidget> ();
			embedded.Initialize (nativeWidget, externalWidget, preferredSizing, reparent);
			return embedded;
		}

		/// <summary>
		/// Wraps a native image object into an Xwt image instance.
		/// </summary>
		/// <returns>The Xwt image containing the native image.</returns>
		/// <param name="nativeImage">The native image.</param>
		public Image WrapImage (object nativeImage)
		{
			return new Image (backend.GetBackendForImage (nativeImage), this);
		}

		/// <summary>
		/// Wraps a native drawing context into an Xwt context object.
		/// </summary>
		/// <returns>The Xwt drawing context.</returns>
		/// <param name="nativeWidget">The native widget to use for drawing.</param>
		/// <param name="nativeContext">The native drawing context.</param>
		public Context WrapContext (object nativeWidget, object nativeContext)
		{
			return new Context (backend.GetBackendForContext (nativeWidget, nativeContext), this);
		}

		public Accessibility.Accessible WrapAccessible (object nativeAccessibleObject)
		{
			return new Accessibility.Accessible (nativeAccessibleObject);
		}

		/// <summary>
		/// Validates that the backend of an Xwt component belongs to the currently loaded toolkit.
		/// </summary>
		/// <returns>The validated Xwt object.</returns>
		/// <param name="obj">The Xwt object.</param>
		/// <exception cref="InvalidOperationException">The component belongs to a different toolkit</exception>
		public object ValidateObject (object obj)
		{
			if (obj is Image)
				((Image)obj).InitForToolkit (this);
			else if (obj is TextLayout)
				((TextLayout)obj).InitForToolkit (this);
			else if (obj is Font) {
				var font = (Font)obj;

				// If the font instance is a system font, we swap instances
				// to not corrupt the backend of the singletons
				if (font.ToolkitEngine != this) {
					var fbh = font.ToolkitEngine.FontBackendHandler;
					if (font.Family == fbh.SystemFont.Family)
						font = FontBackendHandler.SystemFont.WithSettings (font);
					if (font.Family == fbh.SystemMonospaceFont.Family)
						font = FontBackendHandler.SystemMonospaceFont.WithSettings (font);
					if (font.Family == fbh.SystemSansSerifFont.Family)
						font = FontBackendHandler.SystemSansSerifFont.WithSettings (font);
					if (font.Family == fbh.SystemSerifFont.Family)
						font = FontBackendHandler.SystemSerifFont.WithSettings (font);
				}

				font.InitForToolkit (this);
				obj = font;
			} else if (obj is Gradient) {
				((Gradient)obj).InitForToolkit (this);
			} else if (obj is IFrontend) {
				if (((IFrontend)obj).ToolkitEngine != this)
					throw new InvalidOperationException ("Object belongs to a different toolkit");
			}
			return obj;
		}

		/// <summary>
		/// Gets a toolkit backend of an Xwt component and validates
		/// that it belongs to the currently loaded toolkit.
		/// </summary>
		/// <returns>The toolkit backend of the Xwt component.</returns>
		/// <param name="obj">The Xwt component.</param>
		/// <exception cref="InvalidOperationException">The component belongs to a different toolkit</exception>
		public object GetSafeBackend (object obj)
		{
			return GetBackend (ValidateObject (obj));
		}

		/// <summary>
		/// Gets a toolkit backend currently used by an Xwt component.
		/// </summary>
		/// <returns>The toolkit backend of the Xwt component.</returns>
		/// <param name="obj">The Xwt component.</param>
		/// <exception cref="InvalidOperationException">The component does not have a backend</exception>
		public static object GetBackend (object obj)
		{
			if (obj is IFrontend)
				return ((IFrontend)obj).Backend;
			else if (obj == null)
				return null;
			else
				throw new InvalidOperationException ("Object doesn't have a backend");
		}

		/// <summary>
		/// Gets the bounds of a native widget in screen coordinates.
		/// </summary>
		/// <returns>The screen bounds relative to <see cref="P:Xwt.Desktop.Bounds"/>.</returns>
		/// <param name="nativeWidget">The native widget.</param>
		/// <exception cref="ArgumentNullException"><paramref name="nativeWidget"/> is <c>null</c>.</exception>
		/// <exception cref="NotSupportedException">This toolkit does not support this operation.</exception>
		/// <exception cref="InvalidOperationException"><paramref name="nativeWidget"/> does not belong to this toolkit.</exception>
		public Rectangle GetScreenBounds (object nativeWidget)
		{
			if (nativeWidget == null)
				throw new ArgumentNullException (nameof(nativeWidget));
			return backend.GetScreenBounds(nativeWidget);
		}

		/// <summary>
		/// Creates an Xwt frontend for a backend.
		/// </summary>
		/// <returns>The Xwt frontend.</returns>
		/// <param name="ob">The toolkit backend.</param>
		/// <typeparam name="T">The frontend Type.</typeparam>
		public T CreateFrontend<T> (object ob)
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Renders the widget into an Xwt Image.
		/// </summary>
		/// <returns>An Xwt Image containing the rendered bitmap.</returns>
		/// <param name="widget">The Widget to render.</param>
		public Image RenderWidget (Widget widget)
		{
			return new Image (backend.RenderWidget (widget), this);
		}

		/// <summary>
		/// Renders an image to the provided native drawing context
		/// </summary>
		/// <param name="nativeWidget">The native widget.</param>
		/// <param name="nativeContext">The native context.</param>
		/// <param name="img">The Image to render.</param>
		/// <param name="x">The destinate x coordinate.</param>
		/// <param name="y">The destinate y coordinate.</param>
		public void RenderImage (object nativeWidget, object nativeContext, Image img, double x, double y)
		{
			ValidateObject (img);
			img.GetFixedSize (); // Ensure that it has a size
			backend.RenderImage (nativeWidget, nativeContext, img.GetImageDescription (this), x, y);
		}

		/// <summary>
		/// Gets the information about Xwt features supported by the toolkit.
		/// </summary>
		/// <value>The supported features.</value>
		public ToolkitFeatures SupportedFeatures {
			get { return backend.SupportedFeatures; }
		}

		/// <summary>
		/// Registers a backend for an Xwt backend interface.
		/// </summary>
		/// <typeparam name="TBackend">The backend Type</typeparam>
		/// <typeparam name="TImplementation">The Xwt interface implemented by the backend.</typeparam>
		public void RegisterBackend<TBackend, TImplementation> () where TImplementation: TBackend
		{
			backend.RegisterBackend<TBackend, TImplementation> ();
		}

		/// <summary>
		/// Gets the stock icon identified by the stock id.
		/// </summary>
		/// <returns>The stock icon.</returns>
		/// <param name="id">The stock identifier.</param>
		internal Image GetStockIcon (string id)
		{
			Image img;
			if (!stockIcons.TryGetValue (id, out img))
				stockIcons [id] = img = ImageBackendHandler.GetStockIcon (id);
			return img;
		}

		internal ContextBackendHandler ContextBackendHandler;
		internal GradientBackendHandler GradientBackendHandler;
		internal TextLayoutBackendHandler TextLayoutBackendHandler;
		internal FontBackendHandler FontBackendHandler;
		internal ClipboardBackend ClipboardBackend;
		internal ImageBuilderBackendHandler ImageBuilderBackendHandler;
		internal ImagePatternBackendHandler ImagePatternBackendHandler;
		internal ImageBackendHandler ImageBackendHandler;
		internal DrawingPathBackendHandler DrawingPathBackendHandler;
		internal DesktopBackend DesktopBackend;
		internal VectorImageRecorderContextHandler VectorImageRecorderContextHandler;
		internal KeyboardHandler KeyboardHandler;
	}

	class NativeWindowFrame: WindowFrame
	{
		public NativeWindowFrame (IWindowFrameBackend backend)
		{
			BackendHost.SetCustomBackend (backend);
		}
	}
	
	[Flags]
	public enum ToolkitFeatures: int
	{
		/// <summary>
		/// Widget opacity/transparancy.
		/// </summary>
		WidgetOpacity = 1,
		/// <summary>
		/// Window opacity/transparancy.
		/// </summary>
		WindowOpacity = 2,
		/// <summary>
		/// All available features
		/// </summary>
		All = WidgetOpacity | WindowOpacity
	}
}

