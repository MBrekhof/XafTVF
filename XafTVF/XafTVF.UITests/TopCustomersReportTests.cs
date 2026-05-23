using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit;

namespace XafTVF.UITests;

// End-to-end verification of the TVF spike. Mirrors test-artifacts/verify_tvf.py.
// Assumes the Blazor host is already running at https://localhost:5001 — start it with:
//   dotnet run --project XafTVF/XafTVF.Blazor.Server
public class TopCustomersReportTests : PageTest
{
    private const string BaseUrl = "https://localhost:5001";
    private static readonly string ArtifactsDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "artifacts");

    // Self-signed dev cert — accept it.
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true,
        ViewportSize = new ViewportSize { Width = 1400, Height = 900 },
    };

    [Fact]
    public async Task TopCustomersReport_RunsAndDrillsThroughViaIcon()
        => await RunReportAndDrillThrough(useRowClick: false, screenshotPrefix: "icon");

    [Fact]
    public async Task TopCustomersReport_RowClickIsInterceptedAndDrillsThrough()
        => await RunReportAndDrillThrough(useRowClick: true, screenshotPrefix: "rowclick");

    [Fact]
    public async Task TopCustomersReport_PredefinedXtraReport_RendersInPreview()
    {
        Directory.CreateDirectory(ArtifactsDir);
        Page.SetDefaultTimeout(20_000);

        var consoleErrors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") consoleErrors.Add(msg.Text);
        };

        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByLabel("User Name").FillAsync("Admin");
        await Page.WaitForTimeoutAsync(300);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log In" }).ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2500);

        // Navigate straight to the Reports list view via XAF's URL routing — bypasses all the
        // nav-clicking flakiness that comes from XAF's persisted user model state.
        await Page.GotoAsync($"{BaseUrl}/ReportDataV2_ListView", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);
        await Shoot("report_01_reports_list");

        // Click the "Top Customers Report" row in the report list.
        var reportRow = Page.Locator(".dxbl-grid td").Filter(new() { HasText = "Top Customers Report" }).First;
        await reportRow.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await reportRow.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);
        await Shoot("report_02_param_dialog");

        // The XAF param DetailView for TopCustomersReportParams should show Top N + Since.
        var topN = Page.GetByLabel("Top N");
        await topN.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await topN.FillAsync("5");
        await Shoot("report_03_params_filled");

        // ReportsV2 renders the parameter DetailView in a modal popup with a "Preview" button.
        var preview = Page.GetByRole(AriaRole.Button, new() { Name = "Preview" });
        await preview.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
        await preview.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // The Blazor report viewer needs more time to render than the regular ListView.
        await Page.WaitForTimeoutAsync(6000);
        await Shoot("report_04_preview");

        // The Blazor report viewer renders the page into a canvas/SVG, so the body's innerText
        // doesn't include the report content. Instead:
        // - confirm the "Top Customers Report" tab opened (i.e. the preview was launched);
        // - confirm no XAF or app-level error surfaced.
        var bodyText = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("Top Customers Report", bodyText);
        Assert.DoesNotContain("1057", bodyText);
        Assert.DoesNotContain("XafTVFModule.CurrentApplication is null", bodyText);
        Assert.DoesNotContain("An error with number", bodyText);

        var xafErrors = consoleErrors.FindAll(m => m.Contains("error with number"));
        Assert.Empty(xafErrors);
    }

    private async Task RunReportAndDrillThrough(bool useRowClick, string screenshotPrefix)
    {
        Directory.CreateDirectory(ArtifactsDir);
        Page.SetDefaultTimeout(20_000);

        // Track XAF console-surfaced errors so we catch the "1057" message even if it doesn't
        // throw at the Playwright layer.
        var consoleErrors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") consoleErrors.Add(msg.Text);
        };

        // ---------- LANDING / LOGIN ----------
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Shoot($"{screenshotPrefix}_01_landing");

        await Page.GetByLabel("User Name").FillAsync("Admin");
        await Page.WaitForTimeoutAsync(300);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log In" }).ClickAsync();

        // Blazor Server uses SignalR — URL stays the same. Wait for ANY post-login element.
        // (We don't wait for "Tools" specifically because XAF's persisted view state may have
        // landed us on a view where the top tab strip is replaced.)
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2500);

        // Normalize starting view via URL routing so the "Tools" tab strip is visible.
        await Page.GotoAsync($"{BaseUrl}/ApplicationUser_ListView", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);
        await Shoot($"{screenshotPrefix}_02_after_login");

        // ---------- OPEN THE TOOLS TAB ----------
        var toolsTab = Page.GetByText("Tools", new() { Exact = true }).First;
        await toolsTab.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await toolsTab.ClickAsync();
        await Page.WaitForTimeoutAsync(800);

        // ---------- CLICK 'Top Customers Report' ----------
        var actionTrigger = Page.GetByText("Top Customers Report", new() { Exact = true }).First;
        await actionTrigger.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
        await actionTrigger.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // ---------- FILL TopN=5, RUN ----------
        await Page.GetByLabel("Top N").FillAsync("5");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);
        await Shoot($"{screenshotPrefix}_06_result_list");

        // ---------- VERIFY RESULT LIST ----------
        var caption = Page.GetByText("Top 5 customers since", new() { Exact = false }).First;
        await caption.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });

        var rowCount = await Page.Locator(".dxbl-grid tr[role='row']:not(.dxbl-grid-header-row)").CountAsync();
        Assert.True(rowCount >= 5, $"Expected at least 5 result rows; saw {rowCount}");

        // ---------- DRILL THROUGH ----------
        if (useRowClick)
        {
            // Click directly on the row's Name cell text — this fires
            // ListViewProcessCurrentObjectController.ProcessCurrentObjectAction, which without
            // our interception would throw XAF error 1057 (non-persistent record can't be
            // opened as DetailView).
            var rowName = Page.Locator(
                ".dxbl-grid tbody tr:not([dxbl-top-virtual-spacer-element]):not([dxbl-bottom-virtual-spacer-element])"
            ).Filter(new() { Has = Page.Locator("button[data-action-name='Open Customer']") }).First;
            await rowName.ClickAsync();
        }
        else
        {
            // Click the per-row icon (RecordEdit category, SelectionDependencyType.RequireSingleObject).
            var rowActions = Page.Locator("button[data-action-name='Open Customer']:visible");
            var visibleCount = await rowActions.CountAsync();
            Assert.True(
                visibleCount >= 5,
                $"Expected 5 visible 'Open Customer' row buttons; saw {visibleCount}"
            );
            await rowActions.First.ClickAsync();
        }
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);
        await Shoot($"{screenshotPrefix}_08_customer_detail");

        // ---------- ASSERT CUSTOMER DETAIL VIEW ----------
        var bodyText = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("1057", bodyText);
        Assert.DoesNotContain("newly created record cannot be shown", bodyText);
        Assert.Contains("Order Date", bodyText);
        Assert.Contains("Total", bodyText);
        Assert.Contains("Name", bodyText);

        // Sanity: no XAF "error N has occurred" console output during the flow.
        var xafErrors = consoleErrors.FindAll(m => m.Contains("error with number") || m.Contains("1057"));
        Assert.Empty(xafErrors);
    }

    private async Task Shoot(string name)
    {
        var path = Path.Combine(ArtifactsDir, $"{name}.png");
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
    }
}
