using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Styling;

namespace FocusOn;

public partial class MainWindow
{
    private readonly ReleaseUpdates updates = new();
    private AppPreferences preferences = new();
    private string? dataDirectory;
    private bool isolatedSettings, applyingSettings, checkingUpdates;
    private Action<AppPreferences>? savePreferences;
    private Func<string, Task>? relocate;
    private Func<string, Task>? installUpdate;
    private ReleaseUpdate? availableUpdate;
    private string? updateStatusKey;
    private object?[] updateStatusArguments = [];

    public void ConfigureSettings(string directory, bool isolated, AppPreferences value, Action<AppPreferences> save,
        Func<string, Task> changeDirectory, Func<string, Task> install)
    {
        dataDirectory = directory; isolatedSettings = isolated; preferences = value; savePreferences = save;
        relocate = changeDirectory; installUpdate = install;
        ApplySettingsLanguage(); ApplySelectedTheme(value.Theme);
    }
    private void InitializeSettings()
    {
        ThemePicker.SelectionChanged += (_, _) =>
        {
            if (applyingSettings || ThemePicker.SelectedItem is not ComboBoxItem { Tag: string theme } || theme == preferences.Theme) return;
            try { var next = preferences with { Theme = theme }; savePreferences?.Invoke(next); preferences = next; ApplySelectedTheme(theme); SettingsFailure(null); }
            catch (Exception e) { ApplySettingsLanguage(); SettingsFailure(Localizer.Get("无法保存配置：{0}", e.Message)); }
        };
        StartupCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (applyingSettings || isolatedSettings) return;
            try { var next = preferences with { StartAtLogin = StartupCheckBox.IsChecked == true }; savePreferences?.Invoke(next); preferences = next; SettingsFailure(null); }
            catch (Exception e) { ApplySettingsLanguage(); SettingsFailure(Localizer.Get("无法保存配置：{0}", e.Message)); }
        };
        GithubButton.Click += async (_, _) => await OpenUrlAsync(ReleaseUpdates.RepositoryUrl);
        ReleasePageButton.Click += async (_, _) => await OpenUrlAsync(availableUpdate?.ReleaseUrl ?? ReleaseUpdates.RepositoryUrl + "/releases/latest");
        OpenDataButton.Click += async (_, _) => { if (dataDirectory is not null) await OpenUrlAsync(new Uri(dataDirectory + Path.DirectorySeparatorChar).AbsoluteUri); };
        BrowseDataButton.Click += async (_, _) =>
        {
            try
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Localizer.Get("选择空的数据目录"), AllowMultiple = false });
                if (folders.Count == 1 && folders[0].TryGetLocalPath() is { } directory) DataDirectoryBox.Text = directory;
            }
            catch (Exception e) { SettingsFailure(Localizer.Get("无法更改数据目录：{0}", Localizer.Translate(e.Message))); }
        };
        ChangeDataButton.Click += async (_, _) =>
        {
            if (relocate is null) return;
            ChangeDataButton.IsEnabled = false;
            try { await relocate(DataDirectoryBox.Text ?? string.Empty); }
            catch (Exception e) { SettingsFailure(Localizer.Get("无法更改数据目录：{0}", Localizer.Translate(e.Message))); }
            finally { if (!stopped) ChangeDataButton.IsEnabled = true; }
        };
        CheckUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();
        DownloadUpdateButton.Click += async (_, _) => await DownloadUpdateAsync();
    }
    public void ApplySelectedTheme(string theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme == "light" && !ReviewTheme.HighContrast ? ThemeVariant.Light : ThemeVariant.Dark;
            ReviewTheme.Apply(app.Resources, ReviewTheme.HighContrast, theme);
        }
        foreach (var row in rows) row.RefreshAppearance();
        TimelineView.InvalidateVisual(); TotalsScrollViewer.InvalidateVisual(); StatisticsView.ApplyLanguage();
        if (view is not null) UpdateTotals(view);
        ShowPage(activePage);
    }
    private void SettingsFailure(string? message) { SettingsError.Text = message; SettingsError.IsVisible = message is not null; }
    private void ApplySettingsLanguage()
    {
        applyingSettings = true;
        try
        {
            foreach (var (text, key) in new (TextBlock, string)[] { (SettingsHeading,"配置"), (AppearanceHeading,"外观"), (LanguageLabel,"界面语言"), (ThemeLabel,"主题"), (BehaviorHeading,"启动与后台运行"), (StorageHeading,"数据保存"), (UpdatesHeading,"更新"), (AboutHeading,"关于") }) text.Text = Localizer.Get(key);
            foreach (var (button, key) in new (Button, string)[] { (SettingsPageButton,"配置"), (OpenDataButton,"打开数据目录"), (BrowseDataButton,"选择目录…"), (ChangeDataButton,"保存并重启"), (CheckUpdatesButton,"检查更新"), (ReleasePageButton,"查看 Release") }) button.Content = Localizer.Get(key);
            DownloadUpdateButton.Content = Localizer.Get(OperatingSystem.IsWindows() ? "下载并安装…" : "下载更新…");
            AppearanceNote.Text = Localizer.Get("配置立即生效并保存；系统高对比度优先于主题选择。");
            BehaviorNote.Text = Localizer.Get(isolatedSettings ? "隔离数据模式不会更改正式环境的登录自启。" : "关闭窗口后继续后台记录；托盘或菜单栏可恢复窗口并退出。不支持托盘时保留可恢复窗口。");
            StorageNote.Text = Localizer.Get("记录仅保存在本机。更改目录会保存、复制并校验数据，然后重启；保留原目录，不合并已有数据。运行中的独立计时将暂停。");
            UpdateNote.Text = Localizer.Get("仅在点击时访问 GitHub 检查正式版本，不上传记录。Windows 下载校验后运行安装程序；其他系统手动替换应用。安装包当前未签名。");
            AboutNote.Text = Localizer.Get("离线前台软件使用记录与独立计时。MIT 许可；第三方声明随发布包提供。");
            VersionText.Text = Localizer.Get("版本 {0} · {1}", ReleaseUpdates.CurrentVersion.ToString(3), System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier);
            StartupCheckBox.Content = Localizer.Get("登录时启动到托盘"); StartupCheckBox.IsChecked = preferences.StartAtLogin; StartupCheckBox.IsEnabled = !isolatedSettings;
            ThemePicker.SelectedIndex = preferences.Theme == "light" ? 1 : 0;
            CurrentDataDirectoryText.Text = dataDirectory is null ? Localizer.Get("数据目录尚未初始化") : Localizer.Get("当前目录：{0}", dataDirectory);
            if (string.IsNullOrWhiteSpace(DataDirectoryBox.Text)) DataDirectoryBox.Text = dataDirectory;
            DataDirectoryBox.Watermark = Localizer.Get("输入完整的新目录路径，或点击选择目录");
            BrowseDataButton.IsEnabled = ChangeDataButton.IsEnabled = OpenDataButton.IsEnabled = dataDirectory is not null;
            AutomationProperties.SetName(ThemePicker, Localizer.Get("主题")); AutomationProperties.SetName(DataDirectoryBox, Localizer.Get("数据保存"));
            if (updateStatusKey is not null) UpdateStatus.Text = Localizer.Get(updateStatusKey, updateStatusArguments);
            if (SettingsError.Text is { } error) SettingsError.Text = Localizer.Translate(error);
        }
        finally { applyingSettings = false; }
    }
    private void SetUpdateStatus(string key, params object?[] args) { updateStatusKey = key; updateStatusArguments = args; UpdateStatus.Text = Localizer.Get(key, args); }
    public async Task CheckForUpdatesAsync()
    {
        if (checkingUpdates || stopped) return;
        checkingUpdates = true; CheckUpdatesButton.IsEnabled = false; DownloadUpdateButton.IsVisible = false; availableUpdate = null;
        SetUpdateStatus("正在检查更新…");
        try
        {
            var update = await updates.CheckAsync(); if (stopped) return;
            availableUpdate = update;
            SetUpdateStatus(update is null ? "当前版本 {0} 没有可用的正式更新。" : "可用版本 {0}", update?.Version.ToString(3) ?? ReleaseUpdates.CurrentVersion.ToString(3));
            DownloadUpdateButton.IsVisible = update is not null;
        }
        catch (Exception e) { if (!stopped) SetUpdateStatus("检查更新失败：{0}", Localizer.Translate(e.Message)); }
        finally { checkingUpdates = false; if (!stopped) CheckUpdatesButton.IsEnabled = true; }
    }
    private async Task DownloadUpdateAsync()
    {
        if (availableUpdate is not { } update || checkingUpdates || stopped) return;
        if (!OperatingSystem.IsWindows()) { await OpenUrlAsync(update.DownloadUrl.AbsoluteUri); return; }
        if (!await MessageDialog.ConfirmAsync(this, Localizer.Get("更新将下载并校验安装包，保存记录后退出并启动安装程序。独立计时将暂停。安装包未签名。是否继续？"), "继续")) return;
        checkingUpdates = true; DownloadUpdateButton.IsEnabled = CheckUpdatesButton.IsEnabled = false; SetUpdateStatus("正在下载并校验更新…");
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "FocusOnUpdates", Guid.NewGuid().ToString("N"));
            var path = await updates.DownloadVerifiedAsync(update, directory);
            if (!stopped && installUpdate is not null) await installUpdate(path);
        }
        catch (Exception e) { if (!stopped) SetUpdateStatus("更新失败：{0}", Localizer.Translate(e.Message)); }
        finally { checkingUpdates = false; if (!stopped) DownloadUpdateButton.IsEnabled = CheckUpdatesButton.IsEnabled = true; }
    }
    private async Task OpenUrlAsync(string url)
    {
        try { if (!await Launcher.LaunchUriAsync(new Uri(url))) SettingsFailure(Localizer.Get("无法打开链接或目录。")); }
        catch (Exception e) { SettingsFailure(Localizer.Get("无法打开链接：{0}", e.Message)); }
    }
}
