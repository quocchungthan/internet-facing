#!/usr/bin/env python3
"""Copy the shared design tokens and favicon from Farm/wwwroot into MailBox/web/static.

The canonical files live in the Farm ASP.NET app and are the only copies tracked in
git. The MailBox copies are build artifacts: they must exist in the docker build
context before `docker compose build`, and they are uploaded to the VPS by the deploy
workflow (Farm/ itself is never rsynced, so the artifacts must travel with MailBox/).

Run from any directory. Requires Python 3.

    python3 MailBox/scripts/stage-shared-assets.py           # write the artifacts
    python3 MailBox/scripts/stage-shared-assets.py --check    # verify, write nothing

Before building the image by hand:

    python3 MailBox/scripts/stage-shared-assets.py && docker compose -f MailBox/compose.yaml up -d --build
"""
import argparse
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = ROOT / 'Farm' / 'wwwroot'
TARGET_ROOT = ROOT / 'MailBox' / 'web' / 'static'

# (source relative to Farm/wwwroot, target relative to MailBox/web/static)
ASSETS = [
    ('css/shared/variables.css', 'shared/variables.css'),
    ('css/shared/base.css', 'shared/base.css'),
    ('favicon.ico', 'favicon.ico'),
]

STAGE_COMMAND = 'python3 MailBox/scripts/stage-shared-assets.py'


def header(source_relative):
    return (
        '/* GENERATED FILE - DO NOT EDIT.\n'
        f'   Staged from Farm/wwwroot/{source_relative} by MailBox/scripts/stage-shared-assets.py.\n'
        f'   Edit the canonical file instead, then re-run: {STAGE_COMMAND}\n'
        ' */\n'
    )


def expected_bytes(source, source_relative):
    """The exact content the staged artifact must have."""
    if source.suffix == '.css':
        body = source.read_text(encoding='utf-8')
        return (header(source_relative) + body).encode('utf-8')
    return source.read_bytes()


def main():
    parser = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        '--check',
        action='store_true',
        help='verify the staged artifacts match the canonical sources without writing',
    )
    args = parser.parse_args()

    missing = [rel for rel, _ in ASSETS if not (SOURCE_ROOT / rel).is_file()]
    if missing:
        for rel in missing:
            print(f'MISSING canonical source: {SOURCE_ROOT / rel}', file=sys.stderr)
        sys.exit(
            'Canonical shared assets are missing from Farm/wwwroot. They are the single '
            'source of truth for every app in this repo; restore them (or fix the path) '
            'before staging.'
        )

    stale = []
    for source_relative, target_relative in ASSETS:
        source = SOURCE_ROOT / source_relative
        target = TARGET_ROOT / target_relative
        wanted = expected_bytes(source, source_relative)

        if args.check:
            if not target.is_file():
                stale.append(f'{target.relative_to(ROOT)} is missing')
            elif target.read_bytes() != wanted:
                stale.append(f'{target.relative_to(ROOT)} differs from Farm/wwwroot/{source_relative}')
            continue

        target.parent.mkdir(parents=True, exist_ok=True)
        if target.is_file() and target.read_bytes() == wanted:
            print('unchanged:', target.relative_to(ROOT))
            continue
        target.write_bytes(wanted)
        print('staged   :', target.relative_to(ROOT))

    if args.check:
        if stale:
            for item in stale:
                print('OUT OF DATE:', item, file=sys.stderr)
            sys.exit(f'Staged shared assets are missing or stale. Fix: {STAGE_COMMAND}')
        print('Staged shared assets are up to date.')


if __name__ == '__main__':
    main()
