using BarcodeScanning;
using CalorieTracker.UI.Scanning;

namespace CalorieTracker.App.Scanning;

/// <summary>Temporary modal for the scanner trial; #14 replaces it.</summary>
public partial class ScanPage : ContentPage
{
    private readonly TaskCompletionSource<ScanResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ScanPage()
    {
        InitializeComponent();
    }

    /// <summary>Completes with the first detection, or Cancelled if the page closes without one.</summary>
    public Task<ScanResult> Result => completion.Task;

    private async void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        var digits = e.BarcodeResults.Select(b => b.RawValue).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        if (digits is not null)
        {
            await CloseAsync(ScanResult.Scanned(digits));
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e) => await CloseAsync(ScanResult.Cancelled);

    /// <summary>Hardware back is a cancel; the modal pops itself.</summary>
    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(ScanResult.Cancelled);
        return true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Camera.CameraEnabled = true;
    }

    protected override void OnDisappearing()
    {
        Camera.CameraEnabled = false;
        completion.TrySetResult(ScanResult.Cancelled);
        base.OnDisappearing();
    }

    private async Task CloseAsync(ScanResult result)
    {
        // Only the first outcome counts; later detections and the back button do nothing once it is set.
        if (!completion.TrySetResult(result))
        {
            return;
        }

        if (Navigation.ModalStack.Contains(this))
        {
            await Navigation.PopModalAsync();
        }

        // The pop has finished and OnDisappearing has switched the camera off; now release the native camera.
        Camera.Handler?.DisconnectHandler();
    }
}
