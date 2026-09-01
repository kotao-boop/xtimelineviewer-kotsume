using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;

namespace XTimelineViewer.Views;

public sealed partial class MainWindow
{
    private void InitializeBossMode()
    {
        BossModeBtn.Visibility = _appSettings.BossModeButtonVisible ? Visibility.Visible : Visibility.Collapsed;
        BossModeImage.Source = null;
        BossModeEmptyText.Visibility = Visibility.Collapsed;
        if (_appSettings.BossModeImagePath is { Length: > 0 } path && File.Exists(path))
        {
            try { BossModeImage.Source = new BitmapImage(new Uri(path)); }
            catch { BossModeEmptyText.Visibility = Visibility.Visible; }
        }
        else BossModeEmptyText.Visibility = Visibility.Visible;
    }

    private void BossModeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_bossModeActive) return;
        _bossModeActive = true;
        BossModeOverlay.Visibility = Visibility.Visible;
        BossModeCloseBtn.Focus(FocusState.Programmatic);
    }

    private void BossModeCloseBtn_Click(object sender, RoutedEventArgs e) => CloseBossMode();

    private void CloseBossMode()
    {
        _bossModeActive = false;
        BossModeOverlay.Visibility = Visibility.Collapsed;
    }

    private void BossModeEscape_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_bossModeActive)
        {
            CloseBossMode();
            args.Handled = true;
            return;
        }
        if (_focusModeActive)
        {
            ExitFocusMode();
            args.Handled = true;
        }
    }
}
