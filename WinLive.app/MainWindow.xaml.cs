using System.Windows;
using System.Windows.Input;
using WinLive.Presentation;

namespace WinLive.app;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        DragMove();

        if (DataContext is WinLiveShellViewModel vm)
        {
            vm.IslandBounds = vm.IslandBounds.WithPosition(Left, Top);

            if (vm.UpdateSettingsCommand.CanExecute(null))
            {
                vm.UpdateSettingsCommand.Execute(null);
            }
        }
    }
}