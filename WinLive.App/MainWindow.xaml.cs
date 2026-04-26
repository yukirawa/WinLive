using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
    private string? _pressedSecondaryActivityId;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyIslandVisibility(animate: false);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as WinLiveShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyIslandVisibility(animate: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WinLiveShellViewModel.IsIslandVisible))
        {
            Dispatcher.Invoke(() => ApplyIslandVisibility(animate: true));
        }

        if (e.PropertyName == nameof(WinLiveShellViewModel.IsExpanded))
        {
            Dispatcher.Invoke(AnimateExpansionPulse);
        }
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedSecondaryActivityId = null;

        if (FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        _pressedSecondaryActivityId = FindTaggedActivityId(e.OriginalSource as DependencyObject);
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
        var pressedSecondaryActivityId = _pressedSecondaryActivityId;
        _pressedSecondaryActivityId = null;

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

        if (DataContext is not WinLiveShellViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsExpanded &&
            !string.IsNullOrWhiteSpace(pressedSecondaryActivityId) &&
            viewModel.SelectActivityCommand.CanExecute(pressedSecondaryActivityId))
        {
            viewModel.SelectActivityCommand.Execute(pressedSecondaryActivityId);
            e.Handled = true;
            return;
        }

        if (viewModel.ToggleExpandCommand.CanExecute(null))
        {
            viewModel.ToggleExpandCommand.Execute(null);
            e.Handled = true;
        }
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

    private static string? FindTaggedActivityId(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is FrameworkElement { Tag: string id } &&
                !string.IsNullOrWhiteSpace(id))
            {
                return id;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void ApplyIslandVisibility(bool animate)
    {
        var visible = _viewModel?.IsIslandVisible == true;
        IsHitTestVisible = visible;

        if (!animate)
        {
            IslandSurface.Opacity = visible ? 1 : 0;
            IslandScale.ScaleX = visible ? 1 : 0.92;
            IslandScale.ScaleY = visible ? 1 : 0.92;
            IslandTranslate.Y = visible ? 0 : 16;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(visible ? 240 : 150);
        IEasingFunction ease = visible
            ? new QuadraticEase { EasingMode = EasingMode.EaseOut }
            : new CubicEase { EasingMode = EasingMode.EaseInOut };

        Animate(IslandSurface, OpacityProperty, visible ? 1 : 0, duration, ease);
        Animate(IslandScale, System.Windows.Media.ScaleTransform.ScaleXProperty, visible ? 1 : 0.92, duration, ease);
        Animate(IslandScale, System.Windows.Media.ScaleTransform.ScaleYProperty, visible ? 1 : 0.92, duration, ease);
        Animate(IslandTranslate, System.Windows.Media.TranslateTransform.YProperty, visible ? 0 : 16, duration, ease);
    }

    private void AnimateExpansionPulse()
    {
        if (_viewModel?.IsIslandVisible != true)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            From = 0.985,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        IslandScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
        IslandScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation.Clone());
    }

    private static void Animate(
        IAnimatable target,
        DependencyProperty property,
        double to,
        TimeSpan duration,
        IEasingFunction easing)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = duration,
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };

        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }
}
