using System.Windows;
using System.Windows.Threading;
using MediaDesk.Services;

namespace MediaDesk;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception error)
                ErrorLog.Record("Unhandled runtime exception", error);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorLog.Record("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLog.Record("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            "MediaDesk encountered an error: " + e.Exception.Message +
            "\n\nDiagnostic log: " + ErrorLog.LogPath,
            "MediaDesk", MessageBoxButton.OK, MessageBoxImage.Error);
        // Preserve a usable UI after a recoverable event-handler error.
        e.Handled = true;
    }
}
