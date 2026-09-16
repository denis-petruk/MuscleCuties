using System.Xml.Linq;

namespace MuscleCuties.Core.Tests.Architecture;

public class PerformanceArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string AppSource = Path.Combine(RepositoryRoot, "src", "MuscleCuties.App");

    private static readonly string[] MainPagePaths =
    [
        Path.Combine("Pages", "Dashboard", "DashboardPage"),
        Path.Combine("Pages", "Cycle", "CyclePage"),
        Path.Combine("Pages", "Workout", "WorkoutPage"),
        Path.Combine("Pages", "Nutrition", "NutritionPage"),
        Path.Combine("Pages", "Profile", "ProfilePage")
    ];

    [Fact]
    public void MainPages_UseSharedLoadSequencing()
    {
        foreach (var pagePath in MainPagePaths)
        {
            var source = File.ReadAllText(Path.Combine(AppSource, $"{pagePath}.xaml.cs"));

            Assert.Contains("BeginPageLoad(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Dispatcher.Dispatch(() => _ = LoadDeferred", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DashboardPage_PreservesTypedDeferredCards()
    {
        var pagePath = Path.Combine(AppSource, "Pages", "Dashboard", "DashboardPage.xaml");
        var document = XDocument.Load(pagePath);
        var deferredTypes = document
            .Descendants()
            .Where(element => element.Name.LocalName == "LazyView")
            .SelectMany(element => element.Attributes())
            .Where(attribute => attribute.Name.LocalName == "TypeArguments")
            .Select(attribute => attribute.Value)
            .ToList();

        Assert.Contains(deferredTypes, value => value.Contains("DashboardPhaseCard", StringComparison.Ordinal));
        Assert.Contains(deferredTypes, value => value.Contains("DashboardWorkoutCard", StringComparison.Ordinal));
        Assert.Contains(deferredTypes, value => value.Contains("ReadinessRecoveryCard", StringComparison.Ordinal));
        Assert.Contains(deferredTypes, value => value.Contains("DashboardNutritionCard", StringComparison.Ordinal));
        Assert.Contains(deferredTypes, value => value.Contains("DashboardTargetsCard", StringComparison.Ordinal));

        var pageSource = File.ReadAllText(
            Path.Combine(AppSource, "Pages", "Dashboard", "DashboardPage.xaml.cs"));
        Assert.Contains("Loaded=\"OnDashboardLoaded\"", File.ReadAllText(pagePath), StringComparison.Ordinal);
        Assert.Contains("BeginDeferredLoad(LoadDeferredCardsAsync)", pageSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardPhaseCard_UsesStaticPhaseIcon()
    {
        var cardPath = Path.Combine(AppSource, "Controls", "Dashboard", "DashboardPhaseCard.xaml");
        var document = XDocument.Load(cardPath);

        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "PhaseVisualView");
        Assert.Contains(
            document.Descendants().Where(element => element.Name.LocalName == "Image"),
            image => image.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Source" &&
                attribute.Value.Contains("PhaseIconSource", StringComparison.Ordinal)));
    }

    [Fact]
    public void CycleCalendar_UsesReusableDateButtons()
    {
        var calendarPath = Path.Combine(AppSource, "Controls", "Cycle", "CycleCalendarCard.xaml");
        var document = XDocument.Load(calendarPath);

        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "CycleMonthGrid");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "FlexLayout");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "DataTemplate");

        var monthGridSource = File.ReadAllText(
            Path.Combine(AppSource, "Controls", "Cycle", "CycleMonthGrid.cs"));
        Assert.Contains("UpdateExistingButtons", monthGridSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Image", monthGridSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CyclePhaseGuide_UsesStaticListIcons()
    {
        var guidePath = Path.Combine(AppSource, "Controls", "Cycle", "CyclePhaseGuideList.xaml");
        var document = XDocument.Load(guidePath);

        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "PhaseVisualView");
        Assert.Contains(
            document.Descendants().Where(element => element.Name.LocalName == "Image"),
            image => image.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Source" &&
                attribute.Value.Contains("IconSource", StringComparison.Ordinal)));
    }

    [Fact]
    public void ProfilePage_VirtualizesSettingsAndDefersPhotoEditor()
    {
        var profilePath = Path.Combine(AppSource, "Pages", "Profile", "ProfilePage.xaml");
        var document = XDocument.Load(profilePath);
        var collectionViews = document
            .Descendants()
            .Where(element => element.Name.LocalName == "CollectionView")
            .ToList();

        Assert.Single(collectionViews);
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "ScrollView");
        Assert.DoesNotContain(document.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "ItemsSource") &&
            element.Name.LocalName != "CollectionView");
        Assert.Contains(
            document.Descendants().Where(element => element.Name.LocalName == "LazyView"),
            lazyView => lazyView.Attributes().Any(attribute =>
                attribute.Name.LocalName == "TypeArguments" &&
                attribute.Value.Contains("ProfileImageCropModal", StringComparison.Ordinal)));
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "ProfileStatsSection");
    }

    [Fact]
    public void WorkoutPage_UsesDirectHeaderAndDemandInitializedSessionModal()
    {
        var workoutPath = Path.Combine(AppSource, "Pages", "Workout", "WorkoutPage.xaml");
        var document = XDocument.Load(workoutPath);

        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "FeaturedWorkoutCard");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "WorkoutPageHeaderCards");
        Assert.DoesNotContain(
            document.Descendants().Where(element => element.Name.LocalName == "LazyView"),
            lazyView => lazyView.Attributes().Any(attribute =>
                attribute.Value.Contains("WorkoutPageHeaderCards", StringComparison.Ordinal)));

        var modalSource = File.ReadAllText(
            Path.Combine(AppSource, "Controls", "Workout", "WorkoutSessionModal.xaml.cs"));
        Assert.Contains("WorkoutSessionModal : AnimatedModalView", modalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public WorkoutSessionModal()", modalSource, StringComparison.Ordinal);
        Assert.Contains("protected override void PrepareForOpen()", modalSource, StringComparison.Ordinal);
        Assert.Contains("if (_hasContent)", modalSource, StringComparison.Ordinal);
        Assert.Contains("InitializeComponent();", modalSource, StringComparison.Ordinal);
    }

    [Fact]
    public void NutritionPage_UsesOneVirtualizedMealSurface()
    {
        var nutritionPath = Path.Combine(AppSource, "Pages", "Nutrition", "NutritionPage.xaml");
        var document = XDocument.Load(nutritionPath);
        var collectionViews = document
            .Descendants()
            .Where(element => element.Name.LocalName == "CollectionView")
            .ToList();

        Assert.Single(collectionViews);
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "ScrollView");
        Assert.DoesNotContain(document.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "ItemsSource") &&
            element.Name.LocalName != "CollectionView");
    }

    [Fact]
    public void MacroProgressGrid_IsCodeBuiltAndPreservesParentBindingContext()
    {
        var xamlPath = Path.Combine(AppSource, "Controls", "Shared", "MacroProgressGrid.xaml");
        Assert.False(File.Exists(xamlPath), "MacroProgressGrid should be code-built with no XAML file");

        var csPath = Path.Combine(AppSource, "Controls", "Shared", "MacroProgressGrid.xaml.cs");
        var source = File.ReadAllText(csPath);

        Assert.DoesNotContain("BindingContext =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeComponent", source, StringComparison.Ordinal);
        Assert.Contains("BindableProperty.Create", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_DoesNotConstructHiddenMainPages()
    {
        var appSource = File.ReadAllText(Path.Combine(AppSource, "App.xaml.cs"));
        var shellSource = File.ReadAllText(Path.Combine(AppSource, "AppShell.xaml.cs"));
        var programSource = File.ReadAllText(Path.Combine(AppSource, "MauiProgram.cs"));

        Assert.DoesNotContain("PrepareMainPagesAsync", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareMainPagesAsync", shellSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareMainPagesAsync", programSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AttachPreparedPage", shellSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_PreloadsDashboardBeforeNavigationAndDefersRemainingPages()
    {
        var appSource = File.ReadAllText(Path.Combine(AppSource, "App.xaml.cs"));
        var programSource = File.ReadAllText(Path.Combine(AppSource, "MauiProgram.cs"));

        AssertPreloadOrder(appSource, "PreloadDashboardAsync", "NavigateFromStartupAsync", "PreloadRemainingAsync");
        AssertPreloadOrder(programSource, "PreloadDashboardAsync", "NavigateAuthenticatedAsync", "PreloadRemainingAsync");
    }

    [Fact]
    public void MainPages_DoNotNestCollectionViewsInsideScrollViews()
    {
        foreach (var pagePath in MainPagePaths)
        {
            var document = XDocument.Load(Path.Combine(AppSource, $"{pagePath}.xaml"));
            var nestedCollectionView = document
                .Descendants()
                .Where(element => element.Name.LocalName == "ScrollView")
                .SelectMany(element => element.Descendants())
                .FirstOrDefault(element => element.Name.LocalName == "CollectionView");

            Assert.True(
                nestedCollectionView is null,
                $"{pagePath}.xaml contains a CollectionView inside a ScrollView.");
        }
    }

    [Fact]
    public void XamlDataTemplates_UseCompiledBindings()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2009/xaml";
        var missingDataTypes = EnumerateSourceFiles(AppSource, "*.xaml")
            .SelectMany(path => XDocument.Load(path)
                .Descendants()
                .Where(element => element.Name.LocalName == "DataTemplate")
                .Where(element => element.Attribute(xaml + "DataType") is null)
                .Select(_ => Path.GetRelativePath(RepositoryRoot, path)))
            .Distinct()
            .OrderBy(path => path)
            .ToList();

        Assert.True(
            missingDataTypes.Count == 0,
            $"DataTemplates without x:DataType: {string.Join(", ", missingDataTypes)}");
    }

    [Fact]
    public void ApplicationSource_DoesNotBlockOnTasks()
    {
        var blockedCalls = EnumerateSourceFiles(Path.Combine(RepositoryRoot, "src"), "*.cs")
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .Where(file =>
                file.Source.Contains(".GetAwaiter().GetResult()", StringComparison.Ordinal) ||
                file.Source.Contains(".Wait()", StringComparison.Ordinal) ||
                file.Source.Contains(".Result", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepositoryRoot, file.Path))
            .OrderBy(path => path)
            .ToList();

        Assert.True(
            blockedCalls.Count == 0,
            $"Synchronous task blocking found in: {string.Join(", ", blockedCalls)}");
    }

    [Fact]
    public void ProductionCode_DoesNotContainPerformanceTracing()
    {
        var violations = EnumerateSourceFiles(Path.Combine(RepositoryRoot, "src"), "*.cs")
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .Where(file => file.Source.Contains("[Performance]", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepositoryRoot, file.Path))
            .OrderBy(path => path)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Performance tracing found in production code: {string.Join(", ", violations)}. " +
            "Move timing instrumentation to architecture tests.");
    }

    [Fact]
    public void PageCodeBehinds_DoNotUseStopwatch()
    {
        var violations = EnumerateSourceFiles(Path.Combine(AppSource, "Pages"), "*.xaml.cs")
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .Where(file => file.Source.Contains("Stopwatch", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepositoryRoot, file.Path))
            .OrderBy(path => path)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Stopwatch found in page code-behinds: {string.Join(", ", violations)}. " +
            "Pages should not contain timing instrumentation.");
    }

    [Fact]
    public void ProductionCode_DoesNotUseLegacyTimingExtensions()
    {
        var legacyMethods = new[] { "InitializeWithTiming", "BindWithTiming", "CreateWithTiming", "WriteTiming" };
        var violations = EnumerateSourceFiles(Path.Combine(RepositoryRoot, "src"), "*.cs")
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return legacyMethods
                    .Where(method => source.Contains(method, StringComparison.Ordinal))
                    .Select(method => $"{Path.GetRelativePath(RepositoryRoot, path)} ({method})");
            })
            .OrderBy(entry => entry)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Legacy timing extensions found: {string.Join(", ", violations)}. " +
            "Use direct InitializeComponent() and BindingContext assignment instead.");
    }

    [Fact]
    public void PageLoadExtensions_DoesNotContainBudgetConstants()
    {
        var source = File.ReadAllText(Path.Combine(AppSource, "Pages", "PageLoadExtensions.cs"));

        Assert.DoesNotContain("BudgetMilliseconds", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InteractionBudget", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AppShell_DoesNotContainNavigationTiming()
    {
        var source = File.ReadAllText(Path.Combine(AppSource, "AppShell.xaml.cs"));

        Assert.DoesNotContain("NavigationBudgetMilliseconds", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_navigationStopwatch", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopwatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModels_DoNotContainPerformanceStopwatches()
    {
        var viewModelRoot = Path.Combine(RepositoryRoot, "src", "MuscleCuties.Core", "ViewModels");
        var violations = EnumerateSourceFiles(viewModelRoot, "*.cs")
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .Where(file =>
                file.Source.Contains("Stopwatch.StartNew()", StringComparison.Ordinal) ||
                file.Source.Contains("Stopwatch.GetTimestamp()", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepositoryRoot, file.Path))
            .OrderBy(path => path)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Stopwatch found in ViewModels: {string.Join(", ", violations)}. " +
            "ViewModels should not contain timing instrumentation.");
    }

    [Fact]
    public void ControlCodeBehinds_DoNotContainPerformanceTracing()
    {
        var violations = EnumerateSourceFiles(Path.Combine(AppSource, "Controls"), "*.cs")
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .Where(file => file.Source.Contains("[Performance]", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepositoryRoot, file.Path))
            .OrderBy(path => path)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Performance tracing found in controls: {string.Join(", ", violations)}. " +
            "Move timing instrumentation to architecture tests.");
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root, string pattern)
    {
        return Directory
            .EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(path => !ContainsDirectory(path, "bin") && !ContainsDirectory(path, "obj"));
    }

    private static void AssertPreloadOrder(
        string source,
        string dashboardPreload,
        string navigation,
        string remainingPreload)
    {
        var dashboardIndex = source.IndexOf(dashboardPreload, StringComparison.Ordinal);
        var navigationIndex = source.IndexOf(navigation, dashboardIndex + 1, StringComparison.Ordinal);
        var remainingIndex = source.IndexOf(remainingPreload, navigationIndex + 1, StringComparison.Ordinal);

        Assert.True(dashboardIndex >= 0, $"Missing {dashboardPreload}.");
        Assert.True(navigationIndex > dashboardIndex, $"{navigation} must follow {dashboardPreload}.");
        Assert.True(remainingIndex > navigationIndex, $"{remainingPreload} must follow {navigation}.");
    }

    private static bool ContainsDirectory(string path, string directory)
    {
        var segment = $"{Path.DirectorySeparatorChar}{directory}{Path.DirectorySeparatorChar}";
        return path.Contains(segment, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MuscleCuties.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the MuscleCuties repository root.");
    }
}
