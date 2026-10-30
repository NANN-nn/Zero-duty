"""Inventory maintained Unity inputs without moving, deleting or importing assets.

Run with Python 3: python ArtSource/Tools/audit_project.py [--output DIRECTORY]
Only the selected output directory is written. Unity metadata and source assets stay intact.
"""
from pathlib import Path
import argparse
import csv
import json
import re
import subprocess
from collections import Counter

ROOT = Path(__file__).resolve().parents[2]


def audit(output: Path) -> dict:
    output.mkdir(parents=True, exist_ok=True)
    assets = sorted(p for p in (ROOT / 'Assets').rglob('*') if p.is_file())
    inputs = [p for p in assets if p.suffix != '.meta']
    extension_counts = Counter(p.suffix.lower() or '(none)' for p in inputs)
    missing_meta = [p.relative_to(ROOT).as_posix() for p in inputs
                    if not Path(str(p) + '.meta').exists()]
    guids = {}
    duplicates = []
    for meta in (p for p in assets if p.suffix == '.meta'):
        match = re.search(r'^guid:\s*([0-9a-f]{32})\s*$', meta.read_text('utf-8-sig'), re.M)
        if not match:
            continue
        guid = match.group(1)
        if guid in guids:
            duplicates.append([guids[guid], meta.relative_to(ROOT).as_posix()])
        else:
            guids[guid] = meta.relative_to(ROOT).as_posix()
    packages = json.loads((ROOT / 'Packages/manifest.json').read_text('utf-8-sig'))['dependencies']
    status = subprocess.run(['git', 'status', '--short'], cwd=ROOT, capture_output=True,
                            text=True, encoding='utf-8', errors='replace', check=True).stdout
    report = {
        'entry_scene': 'Assets/Resources/Scenes/first.unity',
        'asset_count_excluding_metadata': len(inputs),
        'bytes_excluding_metadata': sum(p.stat().st_size for p in inputs),
        'extension_counts': dict(sorted(extension_counts.items())),
        'missing_metadata': missing_meta,
        'duplicate_asset_guids': duplicates,
        'packages': packages,
        'local_changes': status.splitlines(),
        'source_register': 'Docs/ASSET_REGISTER.md',
        'ending_presentation': 'Static images and captions; no AI ending/failure videos.',
    }
    with (output / 'asset_inventory.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['asset_path', 'extension', 'bytes', 'has_metadata'])
        for path in inputs:
            writer.writerow([path.relative_to(ROOT).as_posix(), path.suffix.lower(),
                             path.stat().st_size, Path(str(path) + '.meta').exists()])
    (output / 'project_audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), 'utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'ArtSource/Documentation')
    result = audit(parser.parse_args().output)
    print(f"Assets: {result['asset_count_excluding_metadata']}; "
          f"missing metadata: {len(result['missing_metadata'])}; "
          f"duplicate GUIDs: {len(result['duplicate_asset_guids'])}")
