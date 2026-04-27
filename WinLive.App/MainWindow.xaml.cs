using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace WinLive.App;

public partial class MainWindow : Window
{
    private const double DragThreshold = 4;

    private WinLiveShellViewModel? _viewModel;
    private Point _dragStartPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private bool _isDragging;
    private bool _dragCaptured;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _viewModel = e.NewValue as WinLiveShellViewModel;
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        _dragStartPoint = e.GetPosition(this);
        _dragStartLeft = Left;
        _dragStartTop = Top;
        _isDragging = false;
        _dragCaptured = IslandSurface.CaptureMouse();
        e.Handled = true;
    }

    private void Island_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragCaptured || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        var deltaX = current.X - _dragStartPoint.X;
        var deltaY = current.Y - _dragStartPoint.Y;

        if (!_isDragging &&
            Math.Abs(deltaX) < DragThreshold &&
            Math.Abs(deltaY) < DragThreshold)
        {
            return;
        }

        _isDragging = true;
        Left = _dragStartLeft + deltaX;
        Top = _dragStartTop + deltaY;
        e.Handled = true;
    }

    private async void Island_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragCaptured)
        {
            IslandSurface.ReleaseMouseCapture();
            _dragCaptured = false;
        }

        if (_isDragging)
        {
            _isDragging = false;
            if (_viewModel is not null)
            {
                await _viewModel.CommitDraggedPositionAsync(Left, Top).ConfigureAwait(true);
            }

            e.Handled = true;
            return;
        }

        if (FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

}
