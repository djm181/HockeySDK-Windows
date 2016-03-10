﻿namespace Microsoft.HockeyApp.Extensibility.Windows
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    
    using Channel;
    using DataContracts;
    using Extensibility;
    using Extensibility.Implementation.Platform;
    using Extensibility.Implementation.Tracing;

#if WINRT || WINDOWS_UWP
    using global::Windows.UI.Xaml;
#endif

    /// <summary>
    /// A module that deals in Exception events and will create ExceptionTelemetry objects when triggered.
    /// </summary>
    internal sealed partial class UnhandledExceptionTelemetryModule : ITelemetryModule, IDisposable
    {
        private TelemetryClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnhandledExceptionTelemetryModule"/> class.
        /// </summary>
        internal UnhandledExceptionTelemetryModule()
        {
        }
        
        internal bool AlwaysHandleExceptions { get; set; }
        
        /// <summary>
        /// Unsubscribe from the <see cref="Application.UnhandledException"/> event.
        /// </summary>
        public void Dispose()
        {
            Application.Current.UnhandledException -= this.ApplicationOnUnhandledException;
        }

        /// <summary>
        /// Subscribes to unhandled event notifications.
        /// </summary>
        public void Initialize(TelemetryConfiguration configuration)
        {
            Application.Current.UnhandledException += this.ApplicationOnUnhandledException;
        }
        
        /// <summary>
        /// Issues with the previous code - 
        /// We were changing the exception as handled which should not be done, 
        /// as the application might want the exception in other unhandled exception event handler.
        /// Re throw of the exception triggers the users unhandled exception event handler twice and also caused the infinite loop issue.
        /// Creating a new thread is not a good practice and the code will eventually move to persist and send exception on resume as hockeyApp.
        /// </summary>
        internal void ApplicationOnUnhandledException(object sender, object e)
        {
            try
            {
#if DEBUG
                global::System.Diagnostics.Debug.WriteLine("UnhandledExceptionTelemetryModule.ApplicationOnUnhandledException started successfully");
#endif
                LazyInitializer.EnsureInitialized(ref this.client, () => { return new TelemetryClient(); });

#if WINRT || WINDOWS_UWP
                UnhandledExceptionEventArgs args = (UnhandledExceptionEventArgs)e;
                Exception eventException = args.Exception;
#elif SILVERLIGHT
                ApplicationUnhandledExceptionEventArgs args = (ApplicationUnhandledExceptionEventArgs)e;
                Exception eventException = args.ExceptionObject;
#endif

#if WINDOWS_UWP
                ITelemetry exceptionTelemetry;
                var crashTelemetry = new CrashTelemetry(eventException);
                crashTelemetry.HandledAt = ExceptionHandledAt.Unhandled;
                exceptionTelemetry = crashTelemetry;
#else
                var exceptionTelemetry = new ExceptionTelemetry(eventException);
                exceptionTelemetry.HandledAt = ExceptionHandledAt.Unhandled;
#endif
                this.client.Track(exceptionTelemetry);
                this.client.Flush();
            }
            catch (Exception ex)
            {
                CoreEventSource.Log.LogError("HockeySDK: An exeption occured in UnhandledExceptionTelemetryModule.ApplicationOnUnhandledException: " + ex);
            }
        }
    }
}