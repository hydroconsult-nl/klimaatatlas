using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Klimaatatlas.Core;
using Klimaatatlas.Gui.Controls;
using Klimaatatlas.Gui.ViewModels;
using Klimaatatlas.Gui.Views;
using Microsoft.Data.Sqlite;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Klimaatatlas.Gui.Tests.TestAppBuilder))]

namespace Klimaatatlas.Gui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class MainWindowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("klimaatatlas-gui-tests-").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static string RepoConfig => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../../../../rekenregels/20241122_rekenregels.json"));

    private static (MainWindow window, MainViewModel vm) Open(string configPath)
    {
        var window = new MainWindow { Width = 1150, Height = 760 };
        var vm = new MainViewModel(new FileDialogs(window), rememberConfiguration: false);
        window.DataContext = vm;
        vm.LoadConfiguration(configPath);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    /// <summary>Saves a rendering of every tab when KLIMAATATLAS_SCREENSHOT_DIR is set (for visual checks).</summary>
    [AvaloniaFact]
    public void ShowsRulesBenchmarksAndColourScales()
    {
        if (!File.Exists(RepoConfig)) return;
        var (window, vm) = Open(RepoConfig);

        Assert.Equal(["biomassa", "dl", "kroos", "oxy"], vm.Rules.Select(r => r.Name));
        Assert.Equal(3, vm.Scenarios.Count);
        Assert.Contains(vm.LogEntries, e => e.IsWarning && e.Message.Contains("bestaat niet")); // c:\SYNC path is not on this machine

        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        var screenshotDir = Environment.GetEnvironmentVariable("KLIMAATATLAS_SCREENSHOT_DIR");
        string[] names = ["doorrekenen", "maatlatten", "rekenregels"];
        for (var i = 0; i < names.Length; i++)
        {
            tabs.SelectedIndex = i;
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (screenshotDir is not null) frame!.Save(Path.Combine(screenshotDir, $"{names[i]}.png"));
            if (i == 1)
            {
                Assert.Equal(vm.SelectedRule!.Benchmarks.Count, window.GetVisualDescendants().OfType<ColorScale>().Count());

                // a rule with a discrete benchmark (doodlopend: ja / nee)
                vm.SelectedRule = vm.Rules.Single(r => r.Name == "kroos");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(vm.SelectedRule.Benchmarks.Count, window.GetVisualDescendants().OfType<ColorScale>().Count());
                if (screenshotDir is not null) window.CaptureRenderedFrame()!.Save(Path.Combine(screenshotDir, "maatlatten-kroos.png"));
            }
        }
    }

    private string CreateTestData(string field = "diepte")
    {
        var gpkg = Path.Combine(_dir, "test.gpkg");
        using (var con = new SqliteConnection($"Data Source={gpkg};Pooling=False"))
        {
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE wv (fid INTEGER PRIMARY KEY, diepte REAL);
                INSERT INTO wv VALUES (1, 1.0), (2, NULL);
                """;
            cmd.ExecuteNonQuery();
        }
        var config = Path.Combine(_dir, "config.json");
        File.WriteAllText(config, """
            { "scenarios": ["A"],
              "features_dataset": { "id": "wv", "path": "test.gpkg", "tablename": "wv", "fields": [] },
              "rules": [ { "name": "r",
                "benchmarks": [ { "name": "d", "classification": "continuous",
                  "fieldname": [ {"scenario":"A","field":"FIELD"} ],
                  "valuesRange": [ {"value":0,"verdict":0}, {"value":2,"verdict":1} ] } ],
                "components": [ { "benchmark": "d", "weight": 1, "resultsfield": "r_d" } ] } ] }
            """.Replace("FIELD", field));
        return config;
    }

    private static void RunAndWait(MainViewModel vm)
    {
        Assert.True(vm.ExecuteCommand.CanExecute(null));
        vm.ExecuteCommand.Execute(null);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while ((vm.IsBusy || vm.ExecuteCommand.IsRunning) && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.False(vm.IsBusy);
    }

    private static void SaveScreenshot(Window window, string name)
    {
        var dir = Environment.GetEnvironmentVariable("KLIMAATATLAS_SCREENSHOT_DIR");
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        if (dir is not null) window.CaptureRenderedFrame()!.Save(Path.Combine(dir, name));
    }

    [AvaloniaFact]
    public void ExecuteWritesResultsAndShowsSuccessBanner()
    {
        var config = CreateTestData();
        var (window, vm) = Open(config);
        Assert.False(vm.HasRunResult);

        RunAndWait(vm);
        SaveScreenshot(window, "klaar-gelukt.png");

        Assert.True(vm.RunSucceeded);
        Assert.Contains("weggeschreven naar test.gpkg", vm.RunSummary);
        Assert.Equal(100, vm.ProgressValue);
        Assert.StartsWith("Klaar: 1 regel(s)", vm.ProgressText);
        Assert.DoesNotContain(vm.LogEntries, e => e.IsError);
        Assert.Contains(vm.LogEntries, e => e.IsWarning && e.Message.Contains("benchmark d has 1 empty value(s)"));
        Assert.True(File.Exists(Path.Combine(_dir, "klimaatatlas.dia")));

        using var check = new SqliteConnection($"Data Source={Path.Combine(_dir, "test.gpkg")};Pooling=False");
        check.Open();
        using var q = check.CreateCommand();
        q.CommandText = "SELECT A_r FROM wv WHERE fid = 1";
        Assert.Equal(0.5, (double)q.ExecuteScalar()!, 9);
    }

    [AvaloniaFact]
    public void FailedRunShowsErrorBanner()
    {
        var (window, vm) = Open(CreateTestData(field: "bestaatniet"));
        RunAndWait(vm);
        SaveScreenshot(window, "klaar-fouten.png");

        Assert.False(vm.RunSucceeded);
        Assert.True(vm.RunFailed);
        Assert.Contains(vm.LogEntries, e => e.IsError && e.Message.Contains("bestaatniet (used by d)"));
    }

    [AvaloniaFact]
    public void GeopackageIsReadOnlyAndOverrideIsVisible()
    {
        var config = CreateTestData();
        var (window, vm) = Open(config);

        var gpkgBox = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Text == vm.GpkgPath);
        Assert.True(gpkgBox.IsReadOnly);
        Assert.False(vm.IsGpkgOverridden);

        var copy = Path.Combine(_dir, "kopie.gpkg");
        File.Copy(Path.Combine(_dir, "test.gpkg"), copy);
        vm.GpkgPath = copy; // what "Andere kiezen…" does after the file picker
        Assert.True(vm.IsGpkgOverridden);
        SaveScreenshot(window, "afwijkende-geopackage.png");

        vm.ResetGpkgCommand.Execute(null);
        Assert.False(vm.IsGpkgOverridden);
        Assert.Equal(Path.Combine(_dir, "test.gpkg"), vm.GpkgPath);
    }

    [Fact]
    public void VerdictColoursRunFromGreenToRed()
    {
        var green = ColorScale.VerdictColor(0);
        var red = ColorScale.VerdictColor(1);
        Assert.True(green.G > green.R);
        Assert.True(red.R > red.G);
        Assert.Equal(ColorScale.VerdictColor(1), ColorScale.VerdictColor(5)); // clamped
    }
}
