using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using MediaDesk.Models;
using MediaDesk.Services;
using Microsoft.Win32;

namespace MediaDesk;

public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly DownloadEngine _engine = new();
    private readonly SemaphoreSlim _slots = new(2, 2);
    private readonly Dictionary<Guid, CancellationTokenSource> _active = [];
    private bool _closing;

    public ObservableCollection<DownloadEntry> Downloads { get; }
    public ObservableCollection<MediaFile> Library { get; } = [];
    public ObservableCollection<string> Favorites { get; }

    public MainWindow()
    {
        _state = StateStore.Load();
        Downloads = new ObservableCollection<DownloadEntry>(_state.Downloads);
        Favorites = new ObservableCollection<string>(_state.Favorites);
        DataContext = this;
        InitializeComponent();
        RefreshLibrary();
        UpdateLanguage();
        Status(IsArabic ? "جاهز — التنزيلات المباشرة فقط" : "Ready — direct URLs only");
    }

    private bool IsArabic => _state.IsArabic;
    private string T(string ar, string en) => IsArabic ? ar : en;
    private void Status(string value) => StatusText.Text = value;

    private void UpdateLanguage()
    {
        FlowDirection = IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        LanguageButton.Content = IsArabic ? "English" : "العربية";
        Subtitle.Text = T("مدير تنزيلات ووسائط لويندوز • إصدار تجريبي مستقل", "Native Windows media downloader • early preview");
        FolderTopButton.Content = T("📁 مجلد التنزيلات", "📁 Download folder");
        AddressHint.Text = T("الصق رابطًا مباشرًا لملف مسموح بتنزيله (HTTP/HTTPS)", "Paste a direct permitted file URL (HTTP/HTTPS)");
        DownloadButton.Content = T("⬇ تنزيل", "⬇ Download");
        SearchButton.Content = T("⌕ بحث في المتصفح", "⌕ Web search");
        DownloadsTab.Header = T("التنزيلات", "Downloads");
        LibraryTab.Header = T("المكتبة", "Library");
        FavoritesTab.Header = T("المفضلة", "Favorites");
        SettingsTab.Header = T("الإعدادات", "Settings");
        PauseButton.Content = T("⏸ إيقاف مؤقت", "⏸ Pause");
        ResumeButton.Content = T("▶ متابعة", "▶ Resume");
        CancelButton.Content = T("✕ إلغاء", "✕ Cancel");
        OpenDownloadButton.Content = T("↗ فتح الملف", "↗ Open file");
        DeleteHistoryButton.Content = T("حذف من القائمة", "Remove entry");
        RefreshButton.Content = T("↻ تحديث المكتبة", "↻ Refresh library");
        PlayButton.Content = T("▶ تشغيل المحدد", "▶ Play selected");
        OpenLibraryButton.Content = T("↗ فتح خارجيًا", "↗ Open externally");
        FavoritesHint.Text = T("احفظ الروابط للعودة إليها. انقر نقرًا مزدوجًا لنقل الرابط إلى الحقل العلوي.",
            "Save links for later. Double-click to copy one into the address field.");
        SaveFavoriteButton.Content = T("☆ إضافة الرابط الحالي", "☆ Save current link");
        RemoveFavoriteButton.Content = T("حذف المحدد", "Remove selected");
        SettingsHeading.Text = T("مجلد حفظ الملفات", "Download folder");
        ChangeFolderButton.Content = T("اختيار مجلد آخر", "Choose another folder");
        LimitNote.Text = T("التنزيل الحالي: ملفات بروابط مباشرة فقط. لا يدعم استخراج روابط من صفحات أو تجاوز DRM.",
            "Current downloader: direct file URLs only. No site extraction or DRM bypass.");
        PlayerNote.Text = T("المشغل يستخدم ترميزات ويندوز المثبتة على النظام.", "Player uses codecs available in Windows.");
        FolderText.Text = _state.DownloadFolder;
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        _state.IsArabic = !_state.IsArabic;
        UpdateLanguage();
        Save();
    }

    private async void Download_Click(object sender, RoutedEventArgs e) => await AddDownloadAsync();
    private async void UrlBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await AddDownloadAsync(); }
    }

    private async Task AddDownloadAsync()
    {
        string url = UrlBox.Text.Trim();
        if (!DownloadEngine.IsSupportedUrl(url))
        {
            MessageBox.Show(this, T("أدخل رابط HTTP أو HTTPS صالحًا لملف مباشر.", "Enter a valid direct HTTP or HTTPS file URL."),
                "MediaDesk", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Uri uri = new(url);
            string filename = DownloadEngine.MakeFileName(uri);
            string target = UniquePath(_state.DownloadFolder, filename);
            var item = new DownloadEntry { Url = url, FileName = Path.GetFileName(target), TargetPath = target };
            Downloads.Insert(0, item);
            Pages.SelectedItem = DownloadsTab;
            Save();
            UrlBox.Clear();
            await RunDownloadAsync(item);
        }
        catch (Exception ex) { ErrorLog.Record("Add download", ex); if (!_closing) ShowError(ex); }
    }

    private static string UniquePath(string directory, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (int i = 0; i < 10000; i++)
        {
            var name = i == 0 ? fileName : $"{baseName} ({i}){ext}";
            var path = Path.Combine(directory, name);
            if (!File.Exists(path) && !File.Exists(path + ".part")) return path;
        }
        throw new IOException("Cannot reserve a unique filename.");
    }

    private async Task RunDownloadAsync(DownloadEntry item)
    {
        if (_closing || _active.ContainsKey(item.Id)) return;
        var cts = new CancellationTokenSource();
        _active[item.Id] = cts;
        bool acquired = false;
        try
        {
            item.State = "Queued";
            await _slots.WaitAsync(cts.Token);
            acquired = true;
            cts.Token.ThrowIfCancellationRequested();
            item.State = "Running";
            var progress = new Progress<DownloadProgress>(p =>
            {
                // Progress<T> dispatches to the WPF UI context created here.
                if (_closing || item.State != "Running") return;
                item.DownloadedBytes = p.Downloaded;
                item.TotalBytes = p.Total;
                item.Speed = p.BytesPerSecond;
            });
            string path = await _engine.DownloadAsync(item, progress, cts.Token);
            item.State = "Completed";
            item.Speed = 0;
            item.DownloadedBytes = new FileInfo(path).Length;
            item.TotalBytes = item.DownloadedBytes;
            RefreshLibrary();
            Status(T("اكتمل: ", "Completed: ") + item.FileName);
        }
        catch (OperationCanceledException)
        {
            if (item.State is not ("Paused" or "Cancelled")) item.State = "Paused";
            item.Speed = 0;
            if (item.State == "Cancelled") TryDeletePartial(item);
        }
        catch (Exception ex)
        {
            item.State = "Failed";
            item.Error = ex.Message;
            item.Speed = 0;
            ErrorLog.Record("Download", ex);
            if (!_closing)
            {
                Status(T("فشل التنزيل: ", "Download failed: ") + ex.Message);
                MessageBox.Show(this,
                    T("تعذر تنزيل الملف:\n", "Download failed:\n") + ex.Message + "\n\n" +
                    T("تأكد من أن الرابط يشير إلى ملف مباشر، وليس صفحة موقع.",
                      "Use a direct file URL, not a web page link."),
                    "MediaDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (acquired) _slots.Release();
            _active.Remove(item.Id);
            cts.Dispose();
            if (!_closing) Save();
        }
    }

    private static void TryDeletePartial(DownloadEntry item)
    {
        try { File.Delete(item.TargetPath + ".part"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private DownloadEntry? SelectedDownload => DownloadsGrid.SelectedItem as DownloadEntry;
    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedDownload;
        if (item == null || !_active.TryGetValue(item.Id, out var token)) return;
        item.State = "Paused";
        token.Cancel();
    }
    private async void Resume_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedDownload;
        if (item == null || _active.ContainsKey(item.Id) || item.State is "Completed" or "Cancelled") return;
        await RunDownloadAsync(item);
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedDownload;
        if (item == null || item.State is "Completed" or "Cancelled") return;
        item.State = "Cancelled";
        if (_active.TryGetValue(item.Id, out var token)) token.Cancel();
        else TryDeletePartial(item);
        Save();
    }
    private void DeleteHistory_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedDownload;
        if (item == null || _active.ContainsKey(item.Id)) return;
        Downloads.Remove(item);
        Save();
    }
    private void OpenDownload_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedDownload;
        if (item == null || item.State != "Completed") return;
        OpenExternal(item.TargetPath);
    }

    private void RefreshLibrary()
    {
        Library.Clear();
        if (!Directory.Exists(_state.DownloadFolder)) return;
        try
        {
            string[] extensions = [".mp4", ".mkv", ".webm", ".mov", ".mp3", ".wav", ".m4a", ".aac", ".flac", ".avi"];
            foreach (var path in Directory.EnumerateFiles(_state.DownloadFolder)
                .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTime).Take(500))
                Library.Add(MediaFile.From(new FileInfo(path)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status(ex.Message); }
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshLibrary();
    private void Play_Click(object sender, RoutedEventArgs e) => PlaySelected();
    private void LibraryGrid_DoubleClick(object sender, MouseButtonEventArgs e) => PlaySelected();
    private void PlaySelected()
    {
        if (LibraryGrid.SelectedItem is not MediaFile entry || !File.Exists(entry.FullPath)) return;
        var player = new PlayerWindow(entry.FullPath) { Owner = this };
        player.Show();
    }
    private void OpenLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (LibraryGrid.SelectedItem is MediaFile file) OpenExternal(file.FullPath);
    }
    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(_state.DownloadFolder); OpenExternal(_state.DownloadFolder); }
        catch (Exception ex) { ShowError(ex); }
    }
    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog
        {
            Title = T("اختر مجلد التنزيلات", "Choose download folder"),
            InitialDirectory = Directory.Exists(_state.DownloadFolder) ? _state.DownloadFolder :
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (picker.ShowDialog(this) != true) return;
        _state.DownloadFolder = picker.FolderName;
        FolderText.Text = _state.DownloadFolder;
        RefreshLibrary();
        Save();
    }

    private void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        string link = UrlBox.Text.Trim();
        if (!DownloadEngine.IsSupportedUrl(link)) { Status(T("أدخل رابطًا صالحًا أولًا", "Enter a valid URL first")); return; }
        if (!Favorites.Contains(link)) Favorites.Insert(0, link);
        Save();
    }
    private void Favorites_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FavoritesList.SelectedItem is not string link) return;
        UrlBox.Text = link;
        UrlBox.Focus();
    }
    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (FavoritesList.SelectedItem is string link) { Favorites.Remove(link); Save(); }
    }
    private void Search_Click(object sender, RoutedEventArgs e)
    {
        string query = UrlBox.Text.Trim();
        if (query.Length == 0) { UrlBox.Focus(); return; }
        if (DownloadEngine.IsSupportedUrl(query)) { OpenExternal(query); return; }
        OpenExternal("https://www.google.com/search?q=" + Uri.EscapeDataString(query));
    }

    private static void OpenExternal(string pathOrUrl)
    {
        try { Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "MediaDesk", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void ShowError(Exception ex) => MessageBox.Show(this, ex.Message, "MediaDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
    private void Save()
    {
        try
        {
            _state.Downloads = Downloads.ToList();
            _state.Favorites = Favorites.ToList();
            StateStore.Save(_state);
        }
        catch (Exception ex) { Status(T("تعذر حفظ الإعدادات: ", "Cannot save settings: ") + ex.Message); }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _closing = true;
        foreach (var item in Downloads.Where(x => x.State is "Running" or "Queued")) item.State = "Paused";
        foreach (var token in _active.Values) token.Cancel();
        Save();
        // Let I/O tasks observe cancellation before process teardown; partial downloads can be resumed next time.
    }
}
