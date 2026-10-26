"""Signs in to a running Obol and screenshots every screen on a computer and a phone.

Usage: python screenshots.py <base-url> <output-folder> [--csv <file>]
In demo mode it signs in as demo / obol-demo (the sample portfolio). Otherwise it creates (or signs in to) the account
Rodas; --csv then imports a Trading 212 CSV export through the Sync screen twice and checks the second import adds
nothing (CI runs this against SQL Server to check the whole path). Fails (exit 1) when a page throws a JavaScript
error, a screen can't reach the engine, or the import goes wrong.
"""
import pathlib
import sys

from playwright.sync_api import sync_playwright

base = sys.argv[1].rstrip("/")
out = pathlib.Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
args = sys.argv[3:]
csv = args[args.index("--csv") + 1] if "--csv" in args else None
errors = []

SCREENS = [
    ("overview", "/"),
    ("overview-all", "/?range=all"),
    ("holdings", "/holdings"),
    ("position", "/holdings/IE00B4L5Y983"),
    ("position-usd", "/holdings/US0378331005"),
    ("insights", "/insights"),
    ("insights-all", "/insights?range=all"),
    ("exposure", "/insights/exposure"),
    ("realized", "/insights/realized"),
    ("goals", "/insights/goals"),
    ("coach", "/insights/coach"),
    ("income", "/income"),
    ("activity", "/activity"),
    ("sync", "/sync"),
    ("settings", "/settings"),
]


def watch(page, label):
    page.on("pageerror", lambda e: errors.append(f"{label}: {e}"))


def sign_in(page):
    page.goto(base + "/sign-in")
    demo = page.locator("[data-demo-fill]")
    if demo.count():
        demo.click()
        page.wait_for_load_state("networkidle")
        return
    page.goto(base + "/create-account")
    if page.locator("input[name=confirmPassword]").count() and not page.locator("input[name=setupKey]").count():
        page.fill("input[name=userName]", "Rodas")
        page.fill("input[name=password]", "portfolio-2026")
        page.fill("input[name=confirmPassword]", "portfolio-2026")
        page.click("button[type=submit]")
        page.wait_for_load_state("networkidle")
    if "/sign-in" in page.url or "/create-account" in page.url:
        page.goto(base + "/sign-in")
        page.fill("input[name=userName]", "Rodas")
        page.fill("input[name=password]", "portfolio-2026")
        page.click("button[type=submit]")
        page.wait_for_load_state("networkidle")
    if "/sign-in" in page.url:
        errors.append("could not sign in")


def import_csv(page, path):
    for attempt, expected in [(1, "added"), (2, "nothing new")]:
        page.goto(base + "/sync")
        page.set_input_files("[data-csv]", path)
        page.wait_for_selector("[data-csv-result].ok, [data-csv-result].err", timeout=60000)
        text = page.inner_text("[data-csv-result]")
        print(f"csv import {attempt}:", text.replace("\n", " | "))
        if "err" in (page.get_attribute("[data-csv-result]", "class") or "") or expected not in text:
            errors.append(f"csv import {attempt}: expected '{expected}', got: {text}")
        if attempt == 1:
            page.screenshot(path=str(out / "desktop-csv-import.png"))


with sync_playwright() as p:
    browser = p.chromium.launch()
    contexts = {
        "desktop": dict(viewport={"width": 1440, "height": 940}),
        "phone": dict(viewport={"width": 390, "height": 844}, device_scale_factor=3, is_mobile=True, has_touch=True),
    }
    for name, options in contexts.items():
        context = browser.new_context(**options)
        page = context.new_page()
        watch(page, name)

        for shot, path in [("landing", "/about"), ("sign-in", "/sign-in"), ("create-account", "/create-account")]:
            page.goto(base + path)
            # The landing page animates in: let it settle.
            page.wait_for_timeout(4500 if shot == "landing" else 1200)
            page.screenshot(path=str(out / f"{name}-{shot}.png"))
            if shot == "landing":
                page.screenshot(path=str(out / f"{name}-{shot}-full.png"), full_page=True)

        sign_in(page)
        if csv and name == "desktop":
            import_csv(page, csv)

        for shot, path in SCREENS:
            response = page.goto(base + path)
            if response is not None and response.status >= 400:
                errors.append(f"{name} {path}: HTTP {response.status}")
            if page.locator("text=Obol's engine isn't answering").count():
                errors.append(f"{name} {path}: the engine didn't answer")
            page.wait_for_timeout(1500)
            page.screenshot(path=str(out / f"{name}-{shot}.png"))
            page.screenshot(path=str(out / f"{name}-{shot}-full.png"), full_page=True)

        # A pie opened on the Overview (the Trading 212 pies, as the app groups them).
        page.goto(base + "/")
        pie = page.locator(".pie-row:visible").first
        if pie.count():
            pie.click()
            page.wait_for_timeout(400)
            page.screenshot(path=str(out / f"{name}-overview-pie.png"), full_page=True)
            page.locator("[data-hview=stocks]").first.click()
            page.wait_for_timeout(300)
            page.screenshot(path=str(out / f"{name}-overview-stocks.png"), full_page=True)
            page.locator("[data-hview=pies]").first.click()

        # The chart cursor, as a finger or the mouse moves over the chart.
        page.goto(base + "/")
        chart = page.locator("[data-chart]").first
        if chart.count():
            box = chart.bounding_box()
            if box:
                page.mouse.move(box["x"] + box["width"] * 0.62, box["y"] + box["height"] * 0.5)
                page.wait_for_timeout(400)
                page.screenshot(path=str(out / f"{name}-overview-cursor.png"))
        context.close()
    browser.close()

for error in errors:
    print("error:", error)
print(f"{len(list(out.glob('*.png')))} screenshots in {out}")
sys.exit(1 if errors else 0)
