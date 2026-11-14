#!/usr/bin/env python3
"""Usage: check-food-names.py [--allow-missing] [--seed cofid-foods.json] [--names names-folder] [--base base-seed.json]
(defaults: the repo's src/CalorieTracker.Core/Seed/cofid-foods.json and its names/ folder)

Checks the everyday food names (naming-guide.md): the seed's `name` of each food, and the names/<foodGroup>.json overlays that
build-food-seed.py applies. A food is named when an overlay has an entry for it (a name equal to originalName is fine).
Prints one finding per line and exits 1 on:
  - a food with no overlay entry (not with --allow-missing), or a seed name that differs from its overlay entry or, with no
    entry, from originalName;
  - an overlay entry that is empty, not in the seed, or in a file that is not named after the food's group;
  - two foods with the same name, case-insensitive and whitespace-normalised (both sourceIds are listed), where at least one
    is named: an unnamed food counts with its current seed name, but two unnamed foods are not compared;
  - a named food whose name has a comma, an American spelling (also plurals and -ing/-ed forms), or more than 80 characters;
  - with --base: a food whose sourceId is not in the base seed, a base food that is gone, or an originalName that differs.
An overlay file with a repeated sourceId key is rejected. Overlay names are stripped once, before every check.
"""
import json
import pathlib
import re
import sys

SEED_DIR = pathlib.Path(__file__).resolve().parents[2] / 'src' / 'CalorieTracker.Core' / 'Seed'
MAX_LENGTH = 80
# The guide's British spelling rule: American word -> what to write.
AMERICAN = {
    'yogurt': 'yoghurt', 'eggplant': 'aubergine', 'zucchini': 'courgette', 'gray': 'grey', 'flavor': 'flavour',
    'flavored': 'flavoured', 'color': 'colour', 'colored': 'coloured', 'fiber': 'fibre', 'savory': 'savoury',
    'cilantro': 'coriander', 'arugula': 'rocket', 'rutabaga': 'swede', 'donut': 'doughnut',
    'chili': 'chilli', 'pasteurized': 'pasteurised',
    'sulfur': 'sulphur',
}
AMERICAN_WORD = re.compile(r'\b(' + '|'.join(AMERICAN) + r')(?:s|es|ing|ed)?\b', re.IGNORECASE)


def normal(name):
    return ' '.join(name.split()).lower()


def no_repeated_keys(pairs):
    keys = [key for key, _ in pairs]
    repeated = sorted({key for key in keys if keys.count(key) > 1})
    if repeated:
        raise ValueError(f'repeated key {", ".join(repeated)}')
    return dict(pairs)


def load_json(path, findings, hook=None):
    try:
        return json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=hook)
    except (OSError, ValueError) as error:
        findings.append(f'{path}: cannot read: {error}')
        return None


def read_overlays(folder, foods, findings):
    """{sourceId: name} from every <group>.json in the folder; a missing folder is no overlays."""
    names = {}
    for path in sorted(folder.glob('*.json')) if folder.is_dir() else []:
        entries = load_json(path, findings, no_repeated_keys)
        if entries is None:
            continue
        if not isinstance(entries, dict):
            findings.append(f'{path.name}: expected an object of sourceId to name')
            continue
        for source_id, name in entries.items():
            food = foods.get(source_id)
            if food is None:
                findings.append(f'{path.name}: {source_id} is not in the seed')
            elif food['foodGroup'] != path.stem:
                findings.append(f'{path.name}: {source_id} is in group {food["foodGroup"]}, not {path.stem}')
            elif not isinstance(name, str) or not name.strip():
                findings.append(f'{path.name}: {source_id} has an empty name')
            else:
                names[source_id] = name.strip()
    return names


def check_base(base_path, foods):
    """Findings for foods whose sourceId set or originalName differs from the base seed's."""
    findings = []
    base = load_json(base_path, findings)
    if base is None:
        return findings
    original = {food['sourceId']: food['originalName'] for food in base['foods']}
    current = {food['sourceId']: food['originalName'] for food in foods}
    for source_id in sorted(current.keys() - original.keys()):
        findings.append(f'{source_id}: not in the base seed')
    for source_id in sorted(original.keys() - current.keys()):
        findings.append(f'{source_id}: in the base seed but not in this one')
    for source_id in sorted(current.keys() & original.keys()):
        if current[source_id] != original[source_id]:
            findings.append(f'{source_id}: originalName "{current[source_id]}" differs from the base "{original[source_id]}"')
    return findings


def check(seed_path, names_folder, allow_missing, base_path=None):
    findings = []
    seed = load_json(seed_path, findings)
    if seed is None:
        return findings
    foods = {food['sourceId']: food for food in seed['foods']}
    overlays = read_overlays(names_folder, foods, findings)

    named, shown = {}, {}
    for source_id in foods:
        if source_id in overlays:
            named.setdefault(normal(overlays[source_id]), []).append(source_id)
            shown.setdefault(normal(overlays[source_id]), overlays[source_id])
    for source_id, food in foods.items():
        if source_id not in overlays:
            if not allow_missing:
                findings.append(f'{source_id}: no everyday name (add it to names/{food["foodGroup"]}.json)')
            elif food['name'] != food['originalName']:
                findings.append(f'{source_id}: seed name "{food["name"]}" has no overlay entry (edit names/, then regenerate)')
            if normal(food['name']) in named:
                named[normal(food['name'])].append(source_id)
            continue
        name = overlays[source_id]
        if food['name'] != name:
            findings.append(f'{source_id}: seed name "{food["name"]}" differs from its overlay "{name}" (regenerate the seed)')
        if ',' in name:
            findings.append(f'{source_id}: "{name}" has a comma')
        for matched, base in sorted({(m.group(0).lower(), m.group(1).lower()) for m in AMERICAN_WORD.finditer(name)}):
            british = AMERICAN[base] + matched[len(base):]
            findings.append(f'{source_id}: "{name}" uses American spelling "{matched}" (British: {british})')
        if len(name) > MAX_LENGTH:
            findings.append(f'{source_id}: "{name}" is {len(name)} characters (at most {MAX_LENGTH})')
    for key, ids in named.items():
        if len(ids) > 1:
            findings.append(f'{" and ".join(ids)}: the same name "{shown[key]}"')
    if base_path is not None:
        findings.extend(check_base(base_path, seed['foods']))
    return findings


def main(argv):
    args = argv[1:]
    allow_missing = '--allow-missing' in args
    args = [a for a in args if a != '--allow-missing']
    options = {'--seed': SEED_DIR / 'cofid-foods.json', '--names': SEED_DIR / 'names', '--base': None}
    while args:
        if len(args) < 2 or args[0] not in options:
            sys.exit(__doc__)
        options[args[0]] = pathlib.Path(args[1])
        args = args[2:]
    findings = check(options['--seed'], options['--names'], allow_missing, options['--base'])
    if findings:
        print('\n'.join(findings))
    return 1 if findings else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
