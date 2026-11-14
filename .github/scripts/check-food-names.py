#!/usr/bin/env python3
"""Usage: check-food-names.py [--allow-missing] [--seed cofid-foods.json] [--names names-folder]
(defaults: the repo's src/CalorieTracker.Core/Seed/cofid-foods.json and its names/ folder)

Checks the everyday food names (naming-guide.md): the seed's `name` of each food, and the names/<foodGroup>.json overlays that
build-food-seed.py applies. A food is named when an overlay has an entry for it (a name equal to originalName is fine).
Prints one finding per line and exits 1 on:
  - a food with no overlay entry (not with --allow-missing), or a seed name that differs from its overlay entry or, with no
    entry, from originalName;
  - an overlay entry that is empty, not in the seed, or in a file that is not named after the food's group;
  - two named foods with the same name, case-insensitive and whitespace-normalised (both sourceIds are listed);
  - a named food whose name has a comma, an American spelling, or more than 80 characters.
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
    'cilantro': 'coriander', 'arugula': 'rocket', 'rutabaga': 'swede', 'molasses': 'treacle', 'donut': 'doughnut',
    'candy': 'sweets', 'cookie': 'biscuit', 'cookies': 'biscuits', 'chili': 'chilli', 'pasteurized': 'pasteurised',
    'sulfur': 'sulphur', 'oatmeal': 'porridge', 'broiled': 'grilled',
}
AMERICAN_WORD = re.compile(r'\b(' + '|'.join(AMERICAN) + r')\b', re.IGNORECASE)


def normal(name):
    return ' '.join(name.split()).lower()


def load_json(path, findings):
    try:
        return json.loads(path.read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        findings.append(f'{path}: cannot read: {error}')
        return None


def read_overlays(folder, foods, findings):
    """{sourceId: name} from every <group>.json in the folder; a missing folder is no overlays."""
    names = {}
    for path in sorted(folder.glob('*.json')) if folder.is_dir() else []:
        entries = load_json(path, findings)
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
                names[source_id] = name
    return names


def check(seed_path, names_folder, allow_missing):
    findings = []
    seed = load_json(seed_path, findings)
    if seed is None:
        return findings
    foods = {food['sourceId']: food for food in seed['foods']}
    overlays = read_overlays(names_folder, foods, findings)

    by_name = {}
    for source_id, food in foods.items():
        if source_id not in overlays:
            if not allow_missing:
                findings.append(f'{source_id}: no everyday name (add it to names/{food["foodGroup"]}.json)')
            elif food['name'] != food['originalName']:
                findings.append(f'{source_id}: seed name "{food["name"]}" has no overlay entry (edit names/, then regenerate)')
            continue
        name = overlays[source_id]
        if food['name'] != name.strip():
            findings.append(f'{source_id}: seed name "{food["name"]}" differs from its overlay "{name}" (regenerate the seed)')
        by_name.setdefault(normal(name), []).append(source_id)
        if ',' in name:
            findings.append(f'{source_id}: "{name}" has a comma')
        for word in sorted({m.lower() for m in AMERICAN_WORD.findall(name)}):
            findings.append(f'{source_id}: "{name}" uses American spelling "{word}" (British: {AMERICAN[word]})')
        if len(name) > MAX_LENGTH:
            findings.append(f'{source_id}: "{name}" is {len(name)} characters (at most {MAX_LENGTH})')
    for ids in by_name.values():
        if len(ids) > 1:
            findings.append(f'{" and ".join(ids)}: the same name "{overlays[ids[0]]}"')
    return findings


def main(argv):
    args = argv[1:]
    allow_missing = '--allow-missing' in args
    args = [a for a in args if a != '--allow-missing']
    options = {'--seed': SEED_DIR / 'cofid-foods.json', '--names': SEED_DIR / 'names'}
    while args:
        if len(args) < 2 or args[0] not in options:
            sys.exit(__doc__)
        options[args[0]] = pathlib.Path(args[1])
        args = args[2:]
    findings = check(options['--seed'], options['--names'], allow_missing)
    if findings:
        print('\n'.join(findings))
    return 1 if findings else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
