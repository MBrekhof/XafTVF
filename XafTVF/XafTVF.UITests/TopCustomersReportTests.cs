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
    public async Task TopCustomersReport_RunsAndDrillsThrough()
    {
        Directory.CreateDirectory(ArtifactsDir);
        Page.SetDefaultTimeout(20_000);

        // ---------- LANDING / LOGIN ----------
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Shoot("01_landing");

        await Page.GetByLabel("User Name").FillAsync("Admin");
        await Page.WaitForTimeoutAsync(300);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log In" }).ClickAsync();

        // Blazor Server uses SignalR — URL stays the same. Wait for the post-login shell.
        var toolsTab = Page.GetByText("Tools", new() { Exact = true }).First;
        await toolsTab.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);
        await Shoot("02_after_login");

        // ---------- OPEN THE TOOLS TAB ----------
        await toolsTab.ClickAsync();
        await Page.WaitForTimeoutAsync(800);
        await Shoot("03_tools_tab");

        // ---------- CLICK 'Top Customers Report' ----------
        var actionTrigger = Page.GetByText("Top Customers Report", new() { Exact = true }).First;
        await actionTrigger.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
        await actionTrigger.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);
        await Shoot("04_popup_open");

        // ---------- FILL TopN=5, RUN ----------
        await Page.GetByLabel("Top N").FillAsync("5");
        await Shoot("05_params_filled");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Run" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);
        await Shoot("06_result_list");

        // ---------- VERIFY RESULT LIST ----------
        var caption = Page.GetByText("Top 5 customers since", new() { Exact = false }).First;
        await caption.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });

        // Count data rows (skip header).
        var rowCount = await Page.Locator(".dxbl-grid tr[role='row']:not(.dxbl-grid-header-row)").CountAsync();
        Assert.True(rowCount >= 5, $"Expected at least 5 result rows; saw {rowCount}");

        // ---------- DRILL THROUGH ----------
        // 'Open Customer' renders as visible per-row buttons (RecordEdit category). The action's
        // SelectionDependencyType.RequireSingleObject means clicking the icon both selects the row
        // and fires the handler. Avoid the hidden toolbar instance by requiring :visible.
        var rowActions = Page.Locator("button[data-action-name='Open Customer']:visible");
        var visibleCount = await rowActions.CountAsync();
        Assert.True(
            visibleCount >= 5,
            $"Expected 5 visible 'Open Customer' row buttons (one per row); saw {visibleCount}"
        );

        await rowActions.First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);
        await Shoot("08_customer_detail");

        // ---------- ASSERT CUSTOMER DETAIL VIEW ----------
        // The drill-through opens a persistent Customer DetailView. Verify by the presence of
        // the Name editor label + the Orders collection's column headers.
        var bodyText = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("Order Date", bodyText);
        Assert.Contains("Total", bodyText);
        Assert.Contains("Name", bodyText);
    }

    private async Task Shoot(string name)
    {
        var path = Path.Combine(ArtifactsDir, $"{name}.png");
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
    }
}
