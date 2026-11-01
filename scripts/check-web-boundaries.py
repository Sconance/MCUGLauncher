from pathlib import Path
import re
import sys
root = Path(__file__).resolve().parent.parent
errors = []
shell = (root / 'src/MCUGLauncher.Avalonia/MainWindow.axaml.cs').read_text(encoding='utf-8-sig')
for token in ('new HttpClient', 'new ConfigStore', 'LaunchService.', 'JsonDocument.Parse', 'File.WriteAllText'):
    if token in shell:
        errors.append(f'MainWindow must delegate application work: {token}')
for source in (root / 'src/MCUGLauncher.App/UseCases').glob('*.cs'):
    text = source.read_text(encoding='utf-8-sig')
    for token in ('using System.Windows', 'Browser.', 'MainWindow', 'new Button', 'new Grid'):
        if token in text:
            errors.append(f'{source.name}: UI dependency {token}')
front = '\n'.join(p.read_text(encoding='utf-8-sig') for p in (root / 'src/MCUGLauncher.Avalonia/Ui').glob('*.js'))
router = (root / 'src/MCUGLauncher.App/UseCases/LauncherApplication.cs').read_text(encoding='utf-8-sig')
commands = set(re.findall(r"(?:post|postMessage)\(\{\s*type:\s*'([^']+)'", front))
handled = set(re.findall(r'case "([^"]+)":', router))
for missing in sorted(commands - handled):
    errors.append(f'Frontend message has no handler: {missing}')
if len(shell.splitlines()) > 300:
    errors.append('MainWindow exceeds shell size budget; review responsibilities before adding code.')
for source in (root / 'src/MCUGLauncher.App').rglob('*.cs'):
    if 'obj' in source.parts or 'bin' in source.parts or source.name == 'MainWindow.axaml.cs':
        continue
    if 'partial class MainWindow' in source.read_text(encoding='utf-8-sig'):
        errors.append(f'Business must not hide in a MainWindow partial: {source.name}')
if 'fetch(' in front:
    errors.append('Frontend transfers must use host commands and the Core network policy.')
for source in (root / 'src/MCUGLauncher.App/Services').glob('*.cs'):
    if 'using MCUGLauncher.UseCases' in source.read_text(encoding='utf-8-sig'):
        errors.append(f'Services must not depend on UI command handlers: {source.name}')
entry = (root / 'src/MCUGLauncher.Avalonia/Ui/app.js').read_text(encoding='utf-8-sig')
for page in ('homePage', 'settingsPage', 'modDetailPage', 'versionManagePage'):
    if f'function {page}(' in entry:
        errors.append(f'Page renderer must stay outside app.js: {page}')
if 'src="preview-data.js"' in (root / 'src/MCUGLauncher.Avalonia/Ui/index.html').read_text(encoding='utf-8-sig'):
    errors.append('Preview fixtures must be loaded conditionally by bootstrap, not by the native index.')
for folder in ('UseCases', 'Services'):
    for source in (root / 'src/MCUGLauncher' / folder).glob('*.cs'):
        non_system_seen = False
        for line in source.read_text(encoding='utf-8-sig').splitlines():
            if not line.startswith('using ') or line.startswith('using static '):
                continue
            if line.startswith('using System.'):
                if non_system_seen:
                    errors.append(f'Put System imports before project imports: {source.name}')
            else:
                non_system_seen = True
auth_service = (root / 'src/MCUGLauncher.App/Authentication/MicrosoftAccountService.cs').read_text(encoding='utf-8-sig')
for dependency in ('new MicrosoftLoginWebUi', 'new MicrosoftLoginWindow', 'CoreWebView2', 'using System.Windows'):
    if dependency in auth_service:
        errors.append(f'Authentication must receive its UI adapter, not construct or probe it: {dependency}')
if errors:
    print('\n'.join(errors), file=sys.stderr)
    sys.exit(1)
print(f'PASS: shell/use-case boundaries and {len(commands)} frontend command names.')
