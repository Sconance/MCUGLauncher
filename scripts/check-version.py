"""Check that one version source stays consistent with docs, code and tags."""
from pathlib import Path
import argparse
import re
import subprocess
import sys

SEMVER = re.compile(r'^\d+\.\d+\.\d+$')
# Match product labels/User-Agent tokens, not repository path segments such as
# /MCUGLauncher/0.22.3-Avalonia in a GitHub raw-content URL.
HARDCODED = re.compile(r'(?<![\w/])(?:MCUGLauncher/|MCUG Launcher\s)\d+\.\d+\.\d+')
XAML_VERSION = re.compile(r'\b\d+\.\d+\.\d+\b')


def read(path):
    return path.read_text(encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description='Verify the single version source.')
    parser.add_argument('--tag', action='store_true', help='also require a v<version> tag on HEAD')
    arguments = parser.parse_args()

    root = Path(__file__).resolve().parent.parent
    errors = []

    props = root / 'Directory.Build.props'
    if not props.is_file():
        print('Directory.Build.props: missing version source', file=sys.stderr)
        return 1
    found = re.search(r'<McugVersion>\s*([^<\s]+)\s*</McugVersion>', read(props))
    if not found:
        print('Directory.Build.props: missing <McugVersion>', file=sys.stderr)
        return 1
    version = found.group(1)
    if not SEMVER.match(version):
        errors.append(f'Directory.Build.props: McugVersion {version!r} is not x.y.z')

    changelog = read(root / 'CHANGELOG.md')
    releases = re.findall(r'^##\s*\[?(\d+\.\d+\.\d+)\]?', changelog, re.MULTILINE)
    if not releases:
        errors.append('CHANGELOG.md: no `## [x.y.z]` release heading')
    elif releases[0] != version:
        errors.append(f'CHANGELOG.md: newest release {releases[0]} does not match McugVersion {version}')
    if version not in releases:
        errors.append(f'CHANGELOG.md: missing a section for {version}')

    readme = read(root / 'README.md')
    documented = re.search(r'当前版本为\s*\*\*(\d+\.\d+\.\d+)\*\*', readme)
    if not documented:
        errors.append('README.md: missing `当前版本为 **x.y.z**`')
    elif documented.group(1) != version:
        errors.append(f'README.md: documented {documented.group(1)} does not match McugVersion {version}')

    for project in sorted(root.glob('*/*/*.csproj')):
        if '<Version>' in read(project):
            errors.append(f'{project.relative_to(root)}: declares <Version>; use McugVersion in Directory.Build.props')

    brand = root / 'src/MCUGLauncher/Brand.cs'
    if brand.is_file() and re.search(r'const\s+string\s+Version', read(brand)):
        errors.append('Brand.cs: version literal found; read ProductInfo.Version instead')

    product = root / 'src/MCUGLauncher.Core/Application/ProductInfo.cs'
    if not product.is_file():
        errors.append('ProductInfo.cs: runtime version source is missing')

    for folder in ('src', 'test', 'tools'):
        for source in (root / folder).rglob('*'):
            if source.suffix not in ('.cs', '.xaml') or any(part in ('bin', 'obj') for part in source.parts):
                continue
            text = read(source)
            if HARDCODED.search(text):
                errors.append(f'{source.relative_to(root)}: hardcoded version; use ProductInfo.Version')
            if source.suffix == '.xaml' and XAML_VERSION.search(text):
                errors.append(f'{source.relative_to(root)}: version literal in markup; bind ProductInfo.Version')

    inline = root / 'test/InlineUiChecks/Program.cs'
    if inline.is_file() and re.search(r'Version!\.ToString\(\)\s*==\s*"\d+\.\d+\.\d+\.0"', read(inline)):
        errors.append('InlineUiChecks: assembly version assertion is hardcoded; compare with assembly metadata')

    build = root / 'scripts/build.ps1'
    if build.is_file():
        text = read(build)
        if 'McugVersion' not in text:
            errors.append('scripts/build.ps1: release naming must read McugVersion from Directory.Build.props')
        if re.search(r'Where-Object\s+Version\b', text):
            errors.append('scripts/build.ps1: reads the removed csproj <Version>; use McugVersion')

    if arguments.tag:
        tags = subprocess.run(['git', 'tag', '--points-at', 'HEAD'], cwd=root, capture_output=True, text=True)
        names = tags.stdout.split() if tags.returncode == 0 else []
        if f'v{version}' not in names:
            errors.append(f'git: HEAD is not tagged v{version}')

    if errors:
        print('\n'.join(errors), file=sys.stderr)
        return 1
    print(f'PASS: version {version} matches Directory.Build.props, CHANGELOG, README and runtime metadata.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
