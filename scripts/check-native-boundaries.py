"""Native shell/use-case boundaries and command coverage (legacy JSON is an internal adapter)."""
from pathlib import Path
import re
import sys
root = Path(__file__).resolve().parent.parent
errors = []
shell = (root / 'src/MCUGLauncher.Avalonia/ShellWindow.axaml.cs').read_text(encoding='utf-8-sig')
for token in ('new HttpClient', 'new ConfigStore', 'LaunchService.', 'JsonDocument.Parse', 'File.WriteAllText'):
    if token in shell: errors.append('ShellWindow must delegate application work: ' + token)
if len(shell.splitlines()) > 200: errors.append('Review ShellWindow responsibilities; keep feature views separate.')
for source in (root / 'src/MCUGLauncher.App').rglob('*.cs'):
    if {'bin','obj'} & set(source.parts): continue
    text = source.read_text(encoding='utf-8-sig')
    for token in ('using System.Windows', 'using Avalonia', 'using Microsoft.Web.WebView2', 'new Button', 'new Grid'):
        if token in text: errors.append(f'{source.name}: backend UI dependency {token}')
    if source.parent.name == 'Services' and 'using MCUGLauncher.UseCases' in text:
        errors.append(f'{source.name}: Services must not depend on command implementations')
# Core must remain reusable without the application configuration or native frontend.
for source in (root / 'src/MCUGLauncher.Core').rglob('*.cs'):
    if {'bin', 'obj'} & set(source.parts): continue
    text = source.read_text(encoding='utf-8-sig')
    for token in ('using MCUGLauncher.Services', 'using MCUGLauncher.UseCases',
                  'using MCUGLauncher.Models', 'using MCUGLauncher.Avalonia',
                  'using Avalonia', 'using System.Windows'):
        if token in text: errors.append(f'{source.name}: Core reverse dependency {token}')
front = (root/'src/MCUGLauncher.App/UseCases/LauncherService.cs').read_text(encoding='utf-8-sig')
router = (root/'src/MCUGLauncher.App/UseCases/LauncherApplication.cs').read_text(encoding='utf-8-sig')
commands = set(re.findall(r'type\s*=\s*"([^"]+)"', front))
handled = set(re.findall(r'case "([^"]+)":', router))
for command in sorted(commands - handled - {'error','pickedPath'}): errors.append('Native command has no handler: '+command)
for source in (root/'src/MCUGLauncher.Avalonia/Views').glob('*.cs'):
    text=source.read_text(encoding='utf-8-sig')
    for token in ('new HttpClient', 'new LauncherContext', 'new LauncherTaskQueue', 'new LauncherApplication', 'JsonDocument.Parse', 'new WebView', 'using MCUGLauncher.Services', 'using MCUGLauncher.UseCases', 'LauncherFacade', 'NativeMessage', 'MessageReceived', 'RequestAsync(', 'SendAsync(', 'JsonSerializer', 'JsonElement', 'AppConfig'):
        if token in text: errors.append(f'{source.name}: view must delegate backend work: {token}')
if (root/'src/MCUGLauncher.Avalonia/Ui').exists(): errors.append('Retired web frontend must not be shipped.')
for source in (root/'src/MCUGLauncher.App/Contracts').glob('*.cs'):
    text = source.read_text(encoding='utf-8-sig')
    for token in ('using Avalonia', 'AppConfig', 'JsonElement', 'JsonDocument', 'CorePreferences', 'Task SendAsync'):
        if token in text: errors.append(f'{source.name}: invalid public contract dependency {token}')
if errors: print('\n'.join(errors),file=sys.stderr);sys.exit(1)
print(f'PASS: native shell, backend boundaries and {len(commands)} command names.')
