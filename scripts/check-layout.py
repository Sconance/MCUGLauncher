from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def main():
    root = Path(__file__).resolve().parent.parent
    errors = []
    projects = sorted(root.glob('src/*/*.csproj'))
    projects += sorted(root.glob('test/*/*.csproj'))
    projects += sorted(root.glob('tools/*/*.csproj'))
    if not projects:
        errors.append('No projects found under src/, test/, or tools/.')
    for project in root.glob('*/*.csproj'):
        errors.append(f'Project outside categorized directories: {project.relative_to(root)}')
    for project in projects:
        try:
            document = ET.parse(project)
        except ET.ParseError as error:
            errors.append(f'{project.relative_to(root)}: {error}')
            continue
        for tag in ('ProjectReference', 'Compile', 'Content', 'Resource', 'EmbeddedResource', 'None'):
            for element in document.iter(tag):
                include = element.get('Include') or element.get('Update')
                if include and '*' not in include and not (project.parent / include).is_file():
                    errors.append(f'{project.relative_to(root)}: missing {tag} {include}')
        for element in document.iter('ApplicationIcon'):
            if element.text and not (project.parent / element.text).is_file():
                errors.append(f'{project.relative_to(root)}: missing icon {element.text}')
        program = project.parent / 'Program.cs'
        if program.is_file() and 'XDocument.Load(' in program.read_text(encoding='utf-8'):
            if not any(element.get('Link') == 'Fixtures/LauncherApp.xaml'
                       and element.get('CopyToOutputDirectory') == 'PreserveNewest'
                       for element in document.iter('Content')):
                errors.append(f'{project.relative_to(root)}: missing copied theme fixture')
    for category in ('src', 'test', 'tools'):
        for source in (root / category).rglob('*.cs'):
            text = source.read_text(encoding='utf-8')
            if 'work/v092/' in text or 'C:/Users/Sconance/' in text:
                errors.append(f'{source.relative_to(root)}: obsolete development path')
    if errors:
        print('\n'.join(errors), file=sys.stderr)
        return 1
    print(f'PASS: {len(projects)} project files; references, assets, and layout paths are valid.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
