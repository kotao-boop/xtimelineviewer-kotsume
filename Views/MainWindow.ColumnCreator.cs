using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using XTimelineViewer.Models;
using XTimelineViewer.Services;

namespace XTimelineViewer.Views
{
    public sealed partial class MainWindow
    {
        private sealed record ColumnTypeChoice(string Id, string Label)
        {
            public override string ToString() => Label;
        }

        private void AddTimelineToolbarBtn_Click(object sender, RoutedEventArgs e)
            => ShowColumnCreatorAsync().FireAndForget(nameof(ShowColumnCreatorAsync));

        private async System.Threading.Tasks.Task ShowColumnCreatorAsync()
        {
            var typeCombo = new ComboBox
            {
                Header = R.Get("ColumnCreator_Type"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[]
                {
                    new ColumnTypeChoice("home", R.Get("Timeline_Home")),
                    new ColumnTypeChoice("notifications", R.Get("Timeline_Notifications")),
                    new ColumnTypeChoice("bookmarks", R.Get("Timeline_Bookmarks")),
                    new ColumnTypeChoice("lists", R.Get("Timeline_Lists")),
                    new ColumnTypeChoice("search", R.Get("ColumnCreator_AdvancedSearch")),
                    new ColumnTypeChoice("profile", R.Get("ColumnCreator_Profile")),
                    new ColumnTypeChoice("explore", R.Get("ColumnCreator_Explore")),
                    new ColumnTypeChoice("custom", R.Get("ColumnCreator_CustomUrl")),
                },
                SelectedIndex = 0,
            };
            var nameBox = new TextBox
            {
                Header = R.Get("Timeline_Name"),
                PlaceholderText = R.Get("Timeline_NameDescription"),
                MaxLength = 80,
            };
            var commonHint = new TextBlock
            {
                Text = R.Get("ColumnCreator_Description"),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
            };

            var allWords = NewCreatorTextBox("SearchBuilder_AllWords");
            var exactPhrase = NewCreatorTextBox("SearchBuilder_ExactPhrase");
            var anyWords = NewCreatorTextBox("SearchBuilder_AnyWords");
            var excludedWords = NewCreatorTextBox("SearchBuilder_ExcludedWords");
            var fromAccount = NewCreatorTextBox("SearchBuilder_FromAccount", "@account");
            var toAccount = NewCreatorTextBox("SearchBuilder_ToAccount", "@account");
            var language = new ComboBox
            {
                Header = R.Get("SearchBuilder_Language"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[]
                {
                    new ColumnTypeChoice("", R.Get("SearchBuilder_AnyLanguage")),
                    new ColumnTypeChoice("ja", R.Get("SearchBuilder_Japanese")),
                    new ColumnTypeChoice("en", R.Get("SearchBuilder_English")),
                },
                SelectedIndex = 0,
            };
            var media = new ComboBox
            {
                Header = R.Get("SearchBuilder_Media"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[]
                {
                    new ColumnTypeChoice("", R.Get("SearchBuilder_AnyMedia")),
                    new ColumnTypeChoice("images", R.Get("SearchBuilder_Images")),
                    new ColumnTypeChoice("videos", R.Get("SearchBuilder_Videos")),
                },
                SelectedIndex = 0,
            };
            var excludeReplies = new CheckBox { Content = R.Get("SearchBuilder_ExcludeReplies") };
            var since = new CalendarDatePicker
            {
                Header = R.Get("SearchBuilder_Since"),
                PlaceholderText = R.Get("SearchBuilder_Optional"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var until = new CalendarDatePicker
            {
                Header = R.Get("SearchBuilder_Until"),
                PlaceholderText = R.Get("SearchBuilder_Optional"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var queryPreview = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 12,
                Opacity = 0.8,
            };
            var searchFields = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = R.Get("SearchBuilder_Title"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 },
                    new TextBlock { Text = R.Get("SearchBuilder_Description"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 },
                    allWords, exactPhrase, anyWords, excludedWords, fromAccount, toAccount,
                    language, media, excludeReplies, since, until,
                    new TextBlock { Text = R.Get("SearchBuilder_Preview"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    queryPreview,
                },
            };

            var profileHandle = NewCreatorTextBox("ColumnCreator_ProfileHandle", "@account");
            var profileFields = new StackPanel
            {
                Spacing = 8,
                Visibility = Visibility.Collapsed,
                Children =
                {
                    new TextBlock { Text = R.Get("ColumnCreator_ProfileDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 },
                    profileHandle,
                },
            };
            var customUrl = NewCreatorTextBox("ColumnCreator_CustomUrl", "https://x.com/...");
            var customFields = new StackPanel
            {
                Spacing = 8,
                Visibility = Visibility.Collapsed,
                Children =
                {
                    new TextBlock { Text = R.Get("ColumnCreator_CustomDescription"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 },
                    customUrl,
                },
            };
            searchFields.Visibility = Visibility.Collapsed;

            var validation = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            };
            var root = new StackPanel
            {
                Spacing = 12,
                MinWidth = 520,
                Children = { commonHint, typeCombo, nameBox, searchFields, profileFields, customFields, validation },
            };
            var dialog = new ContentDialog
            {
                Title = R.Get("ColumnCreator_Title"),
                Content = new ScrollViewer
                {
                    Content = root,
                    MaxHeight = 620,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
                PrimaryButtonText = R.Get("ColumnCreator_Add"),
                CloseButtonText = R.Get("Button_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };

            string BuildAdvancedQuery()
            {
                var selectedMedia = (media.SelectedItem as ColumnTypeChoice)?.Id ?? "";
                return AdvancedSearchQueryBuilder.Build(new(
                    AllWords: allWords.Text,
                    ExactPhrase: exactPhrase.Text,
                    AnyWords: anyWords.Text,
                    ExcludedWords: excludedWords.Text,
                    FromAccount: fromAccount.Text,
                    ToAccount: toAccount.Text,
                    Language: (language.SelectedItem as ColumnTypeChoice)?.Id ?? "",
                    ImagesOnly: selectedMedia == "images",
                    VideosOnly: selectedMedia == "videos",
                    ExcludeReplies: excludeReplies.IsChecked == true,
                    Since: since.Date,
                    Until: until.Date));
            }

            void RefreshState()
            {
                var kind = (typeCombo.SelectedItem as ColumnTypeChoice)?.Id ?? "home";
                searchFields.Visibility = kind == "search" ? Visibility.Visible : Visibility.Collapsed;
                profileFields.Visibility = kind == "profile" ? Visibility.Visible : Visibility.Collapsed;
                customFields.Visibility = kind == "custom" ? Visibility.Visible : Visibility.Collapsed;
                validation.Text = string.Empty;
                var valid = true;
                if (kind == "search")
                {
                    var query = BuildAdvancedQuery();
                    queryPreview.Text = query.Length == 0 ? R.Get("SearchBuilder_EmptyPreview") : query;
                    valid = query.Length > 0;
                    if (!valid) validation.Text = R.Get("SearchBuilder_Required");
                    if (since.Date is { } from && until.Date is { } to && to < from)
                    {
                        valid = false;
                        validation.Text = R.Get("SearchBuilder_DateError");
                    }
                }
                else if (kind == "profile")
                {
                    valid = IsValidXHandle(profileHandle.Text);
                    if (!valid) validation.Text = R.Get("ColumnCreator_ProfileError");
                }
                else if (kind == "custom")
                {
                    valid = UrlHelper.IsXUrl(customUrl.Text.Trim());
                    if (!valid) validation.Text = R.Get("ColumnCreator_UrlError");
                }
                dialog.IsPrimaryButtonEnabled = valid;
            }

            typeCombo.SelectionChanged += (_, _) => RefreshState();
            foreach (var box in new[] { allWords, exactPhrase, anyWords, excludedWords, fromAccount, toAccount, profileHandle, customUrl })
                box.TextChanged += (_, _) => RefreshState();
            language.SelectionChanged += (_, _) => RefreshState();
            media.SelectionChanged += (_, _) => RefreshState();
            excludeReplies.Checked += (_, _) => RefreshState();
            excludeReplies.Unchecked += (_, _) => RefreshState();
            since.DateChanged += (_, _) => RefreshState();
            until.DateChanged += (_, _) => RefreshState();
            RefreshState();

            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;

            var selectedKind = (typeCombo.SelectedItem as ColumnTypeChoice)?.Id ?? "home";
            TimelineConfig config;
            switch (selectedKind)
            {
                case "notifications": config = CreateDefaultConfig(NotificationsTimelineUrl); break;
                case "bookmarks": config = CreateDefaultConfig(BookmarksTimelineUrl); break;
                case "lists":
                    var profile = _profiles.FirstOrDefault(p => p.Id == SelectedToolbarProfileId)
                        ?? _profiles.FirstOrDefault(p => p.Id != "default");
                    if (profile is null) return;
                    config = CreateDefaultConfig(BuildListsUrl(ResolveProfileHandle(profile)));
                    config.ProfileId = profile.Id;
                    config.IsListsIndex = true;
                    break;
                case "search":
                    var query = BuildAdvancedQuery();
                    config = CreateDefaultConfig(SearchQueryHelper.BuildSearchUrl(query));
                    AddSavedSearchQuery(SearchQueryHelper.ExtractSearchPath(config.Url)!);
                    break;
                case "profile":
                    config = CreateDefaultConfig("https://x.com/" + profileHandle.Text.Trim().TrimStart('@'));
                    break;
                case "explore": config = CreateDefaultConfig("https://x.com/explore"); break;
                case "custom": config = CreateDefaultConfig(customUrl.Text.Trim()); break;
                default: config = CreateDefaultConfig(HomeTimelineUrl); break;
            }
            config.Name = nameBox.Text.Trim();
            AddTimeline(config);
        }

        private TextBox NewCreatorTextBox(string headerKey, string? placeholder = null)
            => new()
            {
                Header = R.Get(headerKey),
                PlaceholderText = placeholder ?? string.Empty,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

        private static bool IsValidXHandle(string value)
        {
            var handle = value.Trim().TrimStart('@');
            return handle.Length is > 0 and <= 15 && handle.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
        }
    }
}
