from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parent.parent
errors = []

if (root / "src/MCUGLauncher.Avalonia/Ui").exists():
    errors.append("Web UI assets must not be shipped: src/MCUGLauncher.Avalonia/Ui still exists.")
for name in ("MainWindow.axaml", "MainWindow.axaml.cs"):
    if (root / "src/MCUGLauncher.Avalonia" / name).exists():
        errors.append(f"Legacy WebView host must not return: {name}")

project = (root / "src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj").read_text(encoding="utf-8-sig")
if "Ui/**" in project:
    errors.append("Avalonia project must not copy the removed Ui assets.")

app = (root / "src/MCUGLauncher.Avalonia/App.axaml.cs").read_text(encoding="utf-8-sig")
if "new ShellWindow()" not in app:
    errors.append("App must start the native ShellWindow.")

shell = (root / "src/MCUGLauncher.Avalonia/ShellWindow.axaml.cs").read_text(encoding="utf-8-sig")
if "ClassicButton" in shell or "MainWindow" in shell:
    errors.append("ShellWindow must not keep the legacy WebView entry.")
for token in ("new HttpClient", "new ConfigStore", "LauncherApplication.", "JsonDocument.Parse", "File.WriteAllText"):
    if token in shell:
        errors.append(f"ShellWindow must delegate application work: {token}")

views = list((root / "src/MCUGLauncher.Avalonia/Views").glob("*.cs"))
for source in views:
    text = source.read_text(encoding="utf-8-sig")
    for token in ("new HttpClient", "new ConfigStore", "LauncherApplication", "JsonDocument.Parse",
                  "Browser.", "CoreWebView2", "HandleAsync("):
        if token in text:
            errors.append(f"{source.name}: view must use LauncherFacade, not {token}")

for source in (root / "src/MCUGLauncher.App/UseCases").glob("*.cs"):
    text = source.read_text(encoding="utf-8-sig")
    for token in ("using System.Windows", "using Avalonia", "Browser.", "MainWindow", "ShellWindow",
                  "WebView2", "new Button", "new Grid"):
        if token in text:
            errors.append(f"{source.name}: use case UI dependency {token}")

for source in (root / "src/MCUGLauncher.App/Services").glob("*.cs"):
    text = source.read_text(encoding="utf-8-sig")
    if "using MCUGLauncher.UseCases" in text:
        errors.append(f"Services must not depend on UI command handlers: {source.name}")

allowed_webview = {"MicrosoftLoginWindow.cs", "MicrosoftLoginWebUi.cs"}
for source in (root / "src/MCUGLauncher.Avalonia").glob("*.cs"):
    text = source.read_text(encoding="utf-8-sig")
    if source.name in allowed_webview:
        continue
    for token in ("Microsoft.Web.WebView2", "CoreWebView2", "WebView2"):
        if token in text:
            errors.append(f"WebView2 is only allowed for Microsoft login: {source.name}")

if (root / "test/WebUiChecks").exists():
    errors.append("Removed browser UI checks must not return: test/WebUiChecks.")

for source in (root / "src/MCUGLauncher.App").rglob("*.cs"):
    if "obj" in source.parts or "bin" in source.parts:
        continue
    if "partial class MainWindow" in source.read_text(encoding="utf-8-sig"):
        errors.append(f"Business must not hide in a MainWindow partial: {source.name}")

if errors:
    print("\n".join(errors), file=sys.stderr)
    sys.exit(1)

print(f"PASS: native shell boundaries; {len(views)} views checked; WebView2 limited to Microsoft login.")
