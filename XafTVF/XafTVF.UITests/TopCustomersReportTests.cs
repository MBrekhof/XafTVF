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

        // Blazor Server uses SignalR — URL stays the same. Wait for the post-login shell.
        var toolsTab = Page.GetByText("Tools", new() { Exact = true }).First;
        await toolsTab.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);
        await Shoot($"{screenshotPrefix}_02_after_login");

        // ---------- OPEN THE TOOLS TAB ----------
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
