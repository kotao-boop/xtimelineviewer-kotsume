using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using XTimelineViewer.Services;
using XTimelineViewer.ViewModels;

namespace XTimelineViewer.Views.Settings
{
    public sealed partial class UserDataPage : Page
    {
        private SettingsWindow? _parent;

        /// <summary>x:Bind のバインディングソース。XAML から参照される。</summary>
        public SettingsViewModel? VM { get; private set; }

        public UserDataPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _parent = e.Parameter as SettingsWindow;
            VM      = _parent?.ViewModel;
            if (VM is not null)
                VM.PropertyChanged += OnViewModelPropertyChanged;
            PopulateUI();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (VM is not null)
                VM.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsViewModel.LanguageIndex):
                    PopulateUI();
                    break;
                case nameof(SettingsViewModel.HasSavedQueries):
                    // 全件削除されたら Expander を畳む（IsEnabled はバインディングで追随）
                    if (VM?.HasSavedQueries == false)
                        SavedQueriesExpander.IsExpanded = false;
                    break;
            }
        }

        private void PopulateUI()
        {
            PageTitle.Text = R.Get("Nav_Data");

            BackupCard.Header      = R.Get("Settings_Backup");
            BackupCard.Description = R.Get("Settings_Backup_Description");
            BackupBtn.Content      = R.Get("Settings_Backup_Save");
            RestoreCard.Header      = R.Get("Settings_Restore");
            RestoreCard.Description = R.Get("Settings_Restore_Description");
            RestoreBtn.Content      = R.Get("Settings_Restore_Open");

            // Export Folder
            ExportFolderCard.Header      = R.Get("Settings_ExportFolder");
            ExportFolderCard.Description = _parent?.SettingsFolder ?? "";
            OpenFolderBtn.Content        = R.Get("Button_OpenFolder");

            // Saved Search Queries
            SavedQueriesExpander.Header      = R.Get("Settings_SavedQueries");
            SavedQueriesExpander.Description = R.Get("Settings_SavedQueries_Description");
            VM?.ReloadSavedQueries(R.Get("Profile_Delete"));
            if (VM?.HasSavedQueries == false)
                SavedQueriesExpander.IsExpanded = false;

            // Related settings
            RelatedHeader.Text      = R.Get("Settings_RelatedSettings");
            ProfilesLinkCard.Header = R.Get("Nav_Profiles");

            Bindings.Update();
        }

        private void SavedQueryDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
                VM?.RemoveSavedQuery(path);
        }

        private void ProfilesLinkCard_Click(object sender, RoutedEventArgs e)
            => _parent?.SelectPage("Profiles");

        private async void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_parent is null) return;
            var folder = _parent.SettingsFolder;
            Directory.CreateDirectory(folder);
            await Windows.System.Launcher.LaunchFolderPathAsync(folder);
        }

        private async void BackupBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_parent is null) return;
            try
            {
                _parent.SaveSettingsOnly?.Invoke();
                var picker = new FileSavePicker
                {
                    SuggestedFileName = $"XTimelineViewer-backup-{DateTime.Now:yyyyMMdd}",
                };
                picker.FileTypeChoices.Add(R.Get("Settings_Backup_FileType"), [".xtvbackup"]);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, _parent.WindowHandle);
                var file = await picker.PickSaveFileAsync();
                if (file is null) return;

                var count = SettingsBackupService.CreateBackup(
                    file.Path, _parent.SettingsFolder, GetAppVersion());
                ShowStatus(
                    InfoBarSeverity.Success,
                    R.Get("Settings_Backup_SuccessTitle"),
                    string.Format(R.Get("Settings_Backup_SuccessBody"), count));
            }
            catch (Exception ex)
            {
                ShowStatus(InfoBarSeverity.Error, R.Get("Settings_Backup_ErrorTitle"), ex.Message);
            }
        }

        private async void RestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_parent is null) return;
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".xtvbackup");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, _parent.WindowHandle);
                var file = await picker.PickSingleFileAsync();
                if (file is null) return;

                var confirm = new ContentDialog
                {
                    Title = R.Get("Settings_Restore_ConfirmTitle"),
                    Content = R.Get("Settings_Restore_ConfirmBody"),
                    PrimaryButtonText = R.Get("Settings_Restore_Confirm"),
                    CloseButtonText = R.Get("Button_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot,
                };
                if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

                _parent.SaveSettingsOnly?.Invoke();
                var result = SettingsBackupService.RestoreBackup(
                    file.Path, _parent.SettingsFolder, GetAppVersion());
                ShowStatus(
                    InfoBarSeverity.Success,
                    R.Get("Settings_Restore_SuccessTitle"),
                    string.Format(R.Get("Settings_Restore_SuccessBody"), result.RestoredFileCount));

                if (_parent.BackupRestored is not null)
                    await _parent.BackupRestored();
            }
            catch (Exception ex)
            {
                ShowStatus(InfoBarSeverity.Error, R.Get("Settings_Restore_ErrorTitle"), ex.Message);
            }
        }

        private static string GetAppVersion()
            => typeof(UserDataPage).Assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                   .InformationalVersion.Split('+')[0]
               ?? "unknown";

        private void ShowStatus(InfoBarSeverity severity, string title, string message)
        {
            BackupStatusBar.Severity = severity;
            BackupStatusBar.Title = title;
            BackupStatusBar.Message = message;
            BackupStatusBar.IsOpen = true;
        }
    }
}
