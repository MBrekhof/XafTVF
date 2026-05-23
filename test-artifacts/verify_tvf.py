"""End-to-end UI verification of the TVF spike.

Logs in as Admin, locates the Top Customers Report action, runs it with
TopN=5, verifies the result list, drills through to a Customer DetailView,
saves screenshots at each milestone, and reports findings.
"""

import sys
from pathlib import Path
from playwright.sync_api import sync_playwright, Page, expect

ART = Path(__file__).parent
URL = "https://localhost:5001"


def shoot(page: Page, name: str) -> None:
    path = ART / f"{name}.png"
    page.screenshot(path=str(path), full_page=True)
    print(f"[shot] {path.name}")


def log(msg: str) -> None:
    print(f"[step] {msg}", flush=True)


def main() -> int:
    findings: list[str] = []

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        ctx = browser.new_context(ignore_https_errors=True, viewport={"width": 1400, "height": 900})
        page = ctx.new_page()
        page.set_default_timeout(20_000)

        # Surface console errors for diagnostics.
        page.on("console", lambda msg: print(f"[console.{msg.type}] {msg.text}") if msg.type in ("error", "warning") else None)
        page.on("pageerror", lambda e: print(f"[pageerror] {e}"))

        log(f"GET {URL}")
        page.goto(URL, wait_until="domcontentloaded")
        page.wait_for_load_state("networkidle")
        shoot(page, "01_landing")

        # ---------- LOGIN ----------
        log("Filling login form")
        # XAF Blazor renders a User/Pass DetailView. Field labels are "User Name" and "Password".
        page.get_by_label("User Name").fill("Admin")
        # Password is empty for default Admin — no need to fill.
        page.wait_for_timeout(300)  # let value propagate before submit
        login_btn = page.get_by_role("button", name="Log In")
        login_btn.click()
        # Blazor Server uses SignalR — URL stays the same. Wait for the post-login shell element.
        page.get_by_role("tab", name="Tools").or_(
            page.locator('a, span, button').filter(has_text="Tools").first
        ).first.wait_for(state="visible", timeout=15_000)
        page.wait_for_load_state("networkidle")
        page.wait_for_timeout(1500)
        shoot(page, "02_after_login")

        # ---------- LOCATE ACTION ----------
        log("Switching to Tools tab")
        # XAF Blazor renders action groups as tabs along the top: Home | View | Tools.
        tools_tab = page.locator('a, span, button').filter(has_text="Tools").first
        tools_tab.click()
        page.wait_for_timeout(800)
        shoot(page, "03_tools_tab")

        log("Clicking 'Top Customers Report'")
        action = page.get_by_text("Top Customers Report", exact=True).first
        action.wait_for(state="visible", timeout=5_000)
        action.click()
        page.wait_for_load_state("networkidle")
        page.wait_for_timeout(2000)
        shoot(page, "04_popup_open")

        # ---------- FILL PARAMS ----------
        log("Setting TopN=5 in popup")
        # The popup has TopN (numeric) and Since (date). Find TopN input — the popup caption is
        # "Top Customers" so we scope to the popup region.
        # Strategy: find the input whose label is "Top N".
        topn = page.get_by_label("Top N").first
        topn.fill("5")
        shoot(page, "05_params_filled")

        # ---------- RUN ----------
        log("Clicking Run")
        run_btn = page.get_by_role("button", name="Run")
        run_btn.click()
        page.wait_for_load_state("networkidle")
        page.wait_for_timeout(1500)  # let the result view finish rendering
        shoot(page, "06_result_list")

        # ---------- VERIFY RESULT ----------
        log("Verifying result list shape (5 rows under 'Top 5 customers since ...' caption)")
        # Caption set by controller: "Top {TopN} customers since {Since:yyyy-MM-dd}".
        caption_visible = page.get_by_text("Top 5 customers since", exact=False).first
        try:
            caption_visible.wait_for(state="visible", timeout=5_000)
            log("Caption rendered")
        except Exception:
            findings.append("Result-view caption not found")

        # Count data rows in the DxDataGrid. The class differs across DX versions; try a few.
        row_selectors = [
            "tr.dxbl-grid-edit-row, tr.dxbl-grid-data-row",
            "tr.dxbl-grid-row",
            ".dxbl-grid tr[role='row']:not(.dxbl-grid-header-row)",
        ]
        row_count = 0
        for sel in row_selectors:
            try:
                cnt = page.locator(sel).count()
                if cnt > 0:
                    row_count = cnt
                    log(f"Row count via {sel!r}: {cnt}")
                    break
            except Exception:
                pass
        if row_count < 5:
            findings.append(f"Expected at least 5 result rows, saw {row_count}")

        # Capture one customer name to verify drill-through goes to the correct customer.
        first_name = ""
        try:
            # Skip virtual spacer rows by requiring a non-empty Name cell.
            name_cells = page.locator(".dxbl-grid tbody tr:not([dxbl-top-virtual-spacer-element]):not([dxbl-bottom-virtual-spacer-element]) td").filter(has_text=" ")
            first_name = (name_cells.first.inner_text() or "").strip()
            log(f"First row name: {first_name!r}")
        except Exception as ex:
            log(f"Could not capture first name: {ex}")

        # ---------- DRILL THROUGH ----------
        log("Selecting first result row (by clicking its Name cell)")
        try:
            # Clicking the Name cell text reliably hits a real row (not a virtual spacer).
            if first_name:
                page.get_by_text(first_name, exact=True).first.click()
            else:
                # Fallback: click any visible td in the grid body
                page.locator(".dxbl-grid tbody td:visible").first.click()
            page.wait_for_timeout(600)
            shoot(page, "07_row_selected")
        except Exception as ex:
            findings.append(f"Could not select first row: {ex}")
            shoot(page, "07_row_select_failed")

        log("Probing DOM for the row-action element")
        # The result view shows a small blue icon next to each row name — that's the rendered
        # Open Customer action. Find what element + selector actually matches.
        probe = page.evaluate("""() => {
            const out = [];
            for (const el of document.querySelectorAll('[data-action-name], [title*="Open Customer"], [aria-label*="Open Customer"]')) {
                const r = el.getBoundingClientRect();
                out.push({
                    tag: el.tagName,
                    cls: el.className?.toString().slice(0, 80),
                    title: el.getAttribute('title'),
                    actionName: el.getAttribute('data-action-name'),
                    visible: r.width > 0 && r.height > 0,
                    x: Math.round(r.x), y: Math.round(r.y),
                    text: (el.innerText || '').slice(0, 40)
                });
            }
            return out;
        }""")
        for p in probe:
            print(f"  {p}")

        log("Clicking 'Open Customer' row-action icon")
        try:
            # Prefer the visible RecordEdit row-action icon over the hidden toolbar button.
            open_target = page.locator('[data-action-name="Open Customer"]:visible').first
            open_target.wait_for(state="visible", timeout=5_000)
            open_target.click()
            page.wait_for_load_state("networkidle")
            page.wait_for_timeout(2000)
            shoot(page, "08_customer_detail")

            after_text = page.locator("body").inner_text()
            if first_name and first_name not in after_text:
                findings.append(f"Drill-through opened a view but customer name {first_name!r} not present")
            if "Order Date" not in after_text and "Orders" not in after_text:
                findings.append("Customer DetailView didn't render the Orders collection")
        except Exception as ex:
            findings.append(f"Drill-through failed: {ex}")
            shoot(page, "08_drillthrough_failed")

        browser.close()

    print("\n=== verification summary ===")
    if findings:
        print("FINDINGS:")
        for f in findings:
            print(f"  - {f}")
        return 1
    print("PASS — all milestones reached, screenshots saved.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
