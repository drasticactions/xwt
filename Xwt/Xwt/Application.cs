// 
// Application.cs
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

using System.Threading.Tasks;
using System.Threading;

namespace Xwt
{
	public static class Application
	{
		static Toolkit toolkit;
		static ToolkitEngineBackend engine;
		static UILoop mainLoop;
		static ITranslationCatalog translationCatalog;

		/// <summary>
		/// Gets the task scheduler of the current engine.
		/// </summary>
		/// <value>The toolkit specific task scheduler.</value>
		/// <remarks>
		/// The Xwt task scheduler marshals every Task to the Xwt GUI thread without concurrency.
		/// </remarks>
		public static TaskScheduler UITaskScheduler {
			get { return Toolkit.CurrentEngine.Scheduler; }
		}

		/// <summary>
		/// The main GUI loop.
		/// </summary>
		/// <value>The main loop.</value>
		public static UILoop MainLoop {
			get { return mainLoop; }
		}

		internal static System.Threading.Thread UIThread {
			get;
			private set;
		}

		public static bool InvokeRequired {
			get {
				return UIThread != Thread.CurrentThread;
			}
		}

		public static void ExecuteOnApplicationThread(Action action, bool invokeAsync = true) {
			if(InvokeRequired) {
				Application.Invoke(action, invokeAsync);
			} else {
				action();
			}
		}

		public static ITranslationCatalog TranslationCatalog {
			get {
				if (translationCatalog == null)
					translationCatalog = new DefaultTranslationCatalog();
				return translationCatalog;
			}
			set {
				translationCatalog = value;
			}
		}

		/// <summary>
		/// Initialize Xwt with the best matching toolkit for the current platform.
		/// </summary>
		public static void Initialize ()
		{
			if (engine != null)
				return;
			Initialize (null);
		}

        /// <summary>
        /// Initialize Xwt with the specified type.
        /// </summary>
        /// <param name="type">The toolkit type.</param>
        public static void Initialize(ToolkitType type)
        {
            Initialize(Toolkit.GetBackendType(type), true);
        }

        public static void Initialize(ToolkitType type, bool initializeToolkit)
		{
			Initialize(Toolkit.GetBackendType(type), initializeToolkit);
			toolkit.Type = type;
		}

		/// <summary>
		/// Initialize Xwt with the specified backend type.
		/// </summary>
		/// <param name="backendType">The <see cref="Type.FullName"/> of the backend type.</param>
		public static void Initialize (string backendType)
		{
			Initialize(backendType, true);
		}

		public static void Initialize (string backendType, bool initializeToolkit)
		{			
			if (engine != null)
				return;

			toolkit = Toolkit.Load (backendType, false, initializeToolkit);
			toolkit.SetActive ();
			engine = toolkit.Backend;
			mainLoop = new UILoop (toolkit);

			UIThread = System.Threading.Thread.CurrentThread;

			toolkit.EnterUserCode ();
		}

		/// <summary>
		/// Initializes Xwt as guest, embedded into an other existing toolkit.
		/// </summary>
		/// <param name="type">The toolkit type.</param>
		public static void InitializeAsGuest (ToolkitType type)
		{
			Initialize (type, false);
			toolkit.ExitUserCode (null);
		}
		
		/// <summary>
		/// Initializes Xwt as guest, embedded into an other existing toolkit.
		/// </summary>
		/// <param name="backendType">The <see cref="Type.FullName"/> of the backend type.</param>
		public static void InitializeAsGuest (string backendType)
		{
			if (backendType == null)
				throw new ArgumentNullException ("backendType");
			Initialize (backendType, true);
			toolkit.ExitUserCode (null);
		}

		public static void InitializeAsGuest(ToolkitType type, bool initializeToolkit)
		{
			InitializeAsGuest(Toolkit.GetBackendType(type), initializeToolkit);
			toolkit.Type = type;
		}

		public static void InitializeAsGuest(string backendType, bool initializeToolkit)
		{
			if (backendType == null)
				throw new ArgumentNullException("backendType");
			Initialize(backendType, initializeToolkit);
			toolkit.ExitUserCode(null);
		}

		/// <summary>
		/// Runs the main Xwt GUI thread.
		/// </summary>
		/// <remarks>
		/// Blocks until the main GUI loop exits. Use <see cref="Application.Exit"/>
		/// to stop the Xwt application.
		/// </remarks>
		public static void Run ()
		{
			if (XwtSynchronizationContext.AutoInstall)
			if (SynchronizationContext.Current == null ||
				(!((engine.IsGuest) || (SynchronizationContext.Current is XwtSynchronizationContext))))
					SynchronizationContext.SetSynchronizationContext (toolkit.SynchronizationContext);

			toolkit.InvokePlatformCode (engine.RunApplication);
		}
		
		/// <summary>
		/// Exits the Xwt application.
		/// </summary>
		public static void Exit ()
		{
			toolkit.InvokePlatformCode (engine.ExitApplication);

			if (SynchronizationContext.Current is XwtSynchronizationContext)
				XwtSynchronizationContext.Uninstall ();
		}

		/// <summary>
		/// Exits the Xwt application.
		/// </summary>
		public static void Exit(int exitCode)
		{
			toolkit.InvokePlatformCode(() => engine.ExitApplication(exitCode));

			if (SynchronizationContext.Current is XwtSynchronizationContext)
				XwtSynchronizationContext.Uninstall();
		}

		/// <summary>
		/// Releases all resources used by the application
		/// </summary>
		/// <remarks>This method must be called before the application process ends</remarks>
		public static void Dispose ()
		{
			ResourceManager.Dispose ();
			Toolkit.DisposeAll ();
		}


		/// <summary>
		/// Invokes an action in the GUI thread
		/// </summary>
		/// <param name='action'>
		/// The action to execute.
		/// </param>
		public static void Invoke (Action action, bool invokeAsync = true)
		{
			Invoke (action, toolkit, invokeAsync);
		}

		internal static void Invoke(Action action, Toolkit targetToolkit, bool invokeAsync) {
			if(action == null)
				throw new ArgumentNullException(nameof(action));

			if(targetToolkit == null)
				targetToolkit = toolkit;

			Action d = delegate () {
				try {
					targetToolkit.EnterUserCode();
					action();
					targetToolkit.ExitUserCode(null);
				} catch(Exception ex) {
					targetToolkit.ExitUserCode(ex);
				}
			};

			if(invokeAsync) {
				targetToolkit.Backend.InvokeAsync (delegate {
				targetToolkit.Invoke (d);
				});
			} else {
				targetToolkit.Backend.Invoke (delegate {
					targetToolkit.Invoke (d);
				});
			}
		}

		/// <summary>
		/// Invokes an action in the GUI thread.
		/// </summary>
		public static Task InvokeAsync(Action action)
		{
			if (action == null)
				throw new ArgumentNullException(nameof (action));

			// Capture the current toolkit. It will be used in the invocation
			var targetToolkit = toolkit;

			var ts = new TaskCompletionSource<int> ();

			Action actionCall = () => {
				try {
					targetToolkit.InvokeAndThrow (action);
					ts.SetResult (0);
				} catch (Exception ex) {
					ts.SetException (ex);
				}
			};

			if (UIThread == Thread.CurrentThread)
				actionCall();
			else
				engine.InvokeAsync(actionCall);
			return ts.Task;
		}

		/// <summary>
		/// Invokes a function in the GUI thread.
		/// </summary>
		public static Task<T> InvokeAsync<T>(Func<T> func)
		{
			if (func == null)
				throw new ArgumentNullException(nameof(func));

			// Capture the current toolkit. It will be used in the invocation
			var targetToolkit = toolkit;

			var ts = new TaskCompletionSource<T>();

			Action funcCall = () => {
				try {
					ts.SetResult (targetToolkit.InvokeAndThrow (func));
				} catch (Exception ex) {
					ts.SetException(ex);
				}
			};

			if (UIThread == Thread.CurrentThread)
				funcCall();
			else
				engine.InvokeAsync(funcCall);
			return ts.Task;
		}
		
		/// <summary>
		/// Invokes an action in the GUI thread after the provided time span
		/// </summary>
		/// <returns>
		/// A timer object
		/// </returns>
		/// <param name='action'>
		/// The action to execute.
		/// </param>
		/// <remarks>
		/// This method schedules the execution of the provided function. The function
		/// must return 'true' if it has to be executed again after the time span, or 'false'
		/// if the timer can be discarded.
		/// The execution of the funciton can be canceled by disposing the returned object.
		/// </remarks>
		public static IDisposable TimeoutInvoke (int ms, Func<bool> action)
		{
			if (action == null)
				throw new ArgumentNullException ("action");
			if (ms < 0)
				throw new ArgumentException ("ms can't be negative");

			return TimeoutInvoke (TimeSpan.FromMilliseconds (ms), action);
		}
		
		/// <summary>
		/// Invokes an action in the GUI thread after the provided time span
		/// </summary>
		/// <returns>
		/// A timer object
		/// </returns>
		/// <param name='action'>
		/// The action to execute.
		/// </param>
		/// <remarks>
		/// This method schedules the execution of the provided function. The function
		/// must return 'true' if it has to be executed again after the time span, or 'false'
		/// if the timer can be discarded.
		/// The execution of the funciton can be canceled by disposing the returned object.
		/// </remarks>
		public static IDisposable TimeoutInvoke (TimeSpan timeSpan, Func<bool> action)
		{
			if (action == null)
				throw new ArgumentNullException (nameof (action));
			if (timeSpan.Ticks < 0)
				throw new ArgumentException ("timeSpan can't be negative");

			// Capture the current toolkit. It will be used in the invocation
			var targetToolkit = Toolkit.CurrentEngine;

			Timer t = new Timer ();
			t.Id = engine.TimerInvoke (delegate {
				return targetToolkit.Invoke (action);
			}, timeSpan);
			return t;
		}

		/// <summary>
		/// Create a toolkit specific status icon.
		/// </summary>
		/// <returns>The status icon.</returns>
		public static StatusIcon CreateStatusIcon ()
		{
			return new StatusIcon ();
		}

		/// <summary>
		/// Occurs when an exception is not caught.
		/// </summary>
		/// <remarks>Subscribe to handle uncaught exceptions, which could
		/// otherwise block or stop the application.</remarks>
		public static event EventHandler<ExceptionEventArgs> UnhandledException;
		
		class Timer: IDisposable
		{
			public object Id;
			public void Dispose ()
			{
				Application.engine.CancelTimerInvoke (Id);
			}
		}

		/// <summary>
		/// Notifies about unhandled exceptions using the UnhandledException event.
		/// </summary>
		/// <param name="ex">The unhandled Exception.</param>
		internal static void NotifyException (Exception ex)
		{
			var unhandledException = UnhandledException;
			if (unhandledException != null)
			{
				unhandledException (null, new ExceptionEventArgs (ex));
			}
			else
			{
				Console.WriteLine (ex);
			}
		}

		public static event EventHandler Activated;
		public static void OnActivated()
		{
			if (Activated != null)
			{
				Activated(null, EventArgs.Empty);
			}
		}

		public static event EventHandler Deactivated;
		public static void OnDeactivated()
		{
			if (Deactivated != null)
			{
				Deactivated(null, EventArgs.Empty);
			}
		}
	}

	/// <summary>
	/// The UILoop class provides access to the main GUI loop.
	/// </summary>
	public class UILoop
	{
		Toolkit toolkit;
		public Toolkit Engine { get { return toolkit; }}

		internal UILoop (Toolkit toolkit)
		{
			this.toolkit = toolkit;
		}

		/// <summary>
		/// Dispatches pending events in the GUI event queue
		/// </summary>
		public void DispatchPendingEvents ()
		{
			Toolkit.CurrentEngine.InvokePlatformCode (delegate {
				toolkit.Backend.DispatchPendingEvents ();
			});
		}

		/// <summary>
		/// Runs an Action after all user handlers have been processed and
		/// the main GUI loop is about to proceed with its next iteration.
		/// </summary>
		/// <param name="action">Action to execute.</param>
		public void QueueExitAction (Action action)
		{
			if (action == null)
				throw new ArgumentNullException ("action");
			toolkit.QueueExitAction (action);
		}
	}


	public enum ToolkitType
	{
		Gtk = 1,
		Cocoa = 2,
		Wpf = 3,
		XamMac = 4,
		Gtk3 = 5,
	}
}

