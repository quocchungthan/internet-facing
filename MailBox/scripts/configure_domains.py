#!/usr/bin/env python3
"""Render separate Maddy configuration arguments for every local mail domain."""
import argparse
from pathlib import Path
import re

DOMAIN = re.compile(r'(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}')
DECLARATION = '$(local_domains) = '


def configure_domains(path, domains):
    if not domains or any(not DOMAIN.fullmatch(domain) for domain in domains):
        raise ValueError('Supply valid lowercase ASCII/punycode domains.')
    if len(set(domains)) != len(domains):
        raise ValueError('Duplicate local mail domain.')
    lines = path.read_text(encoding='utf-8').splitlines(keepends=True)
    if sum(line.startswith(DECLARATION) for line in lines) != 1:
        raise ValueError('Expected exactly one local_domains declaration in maddy.conf.')
    rendered = ''.join(
        DECLARATION + ' '.join(domains) + '\n' if line.startswith(DECLARATION) else line
        for line in lines
    )
    path.write_text(rendered, encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('domains', nargs='+')
    args = parser.parse_args()
    try:
        configure_domains(Path(__file__).resolve().parents[1] / 'maddy' / 'maddy.conf', args.domains)
    except ValueError as error:
        parser.error(str(error))
    print('Configured local mail domains:', ' '.join(args.domains))


if __name__ == '__main__':
    main()
