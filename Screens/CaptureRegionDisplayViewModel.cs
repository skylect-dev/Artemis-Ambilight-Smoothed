using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Artemis.Plugins.LayerBrushes.AmbilightSmoothed.PropertyGroups;
using Artemis.UI.Shared;
using Avalonia.Controls;
using ReactiveUI;
using ScreenCapture.NET;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Screens;

public class CaptureRegionDisplayViewModel : ActivatableViewModelBase
{
    private Image? _previewImage;

    public CaptureRegionDisplayViewModel(Display display, AmbilightSmoothedCaptureProperties properties)
    {
        Display = display;
        Properties = properties;
        DisplayPreview = new DisplayPreview(display, properties);
        this.WhenActivated(d => Disposable.Create(() => DisplayPreview.Dispose()).DisposeWith(d));
    }

    public Display Display { get; }
    public AmbilightSmoothedCaptureProperties Properties { get; }
    public DisplayPreview DisplayPreview { get; }

    public Image? PreviewImage
    {
        get => _previewImage;
        set => RaiseAndSetIfChanged(ref _previewImage, value);
    }

    public void Update()
    {
        DisplayPreview?.Update();
        PreviewImage?.InvalidateVisual();
    }
}