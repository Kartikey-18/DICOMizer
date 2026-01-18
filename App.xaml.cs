using System.Windows;

namespace DICOMizer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize logging and configuration
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        MessageBox.Show(
            $"An unexpected error occurred: {exception?.Message}\n\nPlease check the log files for details.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
