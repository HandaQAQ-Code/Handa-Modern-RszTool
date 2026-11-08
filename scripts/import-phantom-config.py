"""Import the supplied PhantomRequiem lists into the launcher's settings and local flag notes.

UVAR is read only. Chapter X-1 requires an explicit group mapping.
"""
import argparse
import datetime
import json
import os
from pathlib import Path
import re
import shutil
import struct
import uuid


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--chapter-x1', choices=['Chapter0302', 'Chapter0303', 'Location47'])
    args = parser.parse_args()
    workspace = Path(__file__).resolve().parents[1]
    mod = Path('D:/SteamLibrary/steamapps/common/RESIDENT EVIL 4  BIOHAZARD RE4/Mdo/mods/PhantomRequiem/natives/stm')
    settings_path = workspace / 'publish/Drop-Flags-Montage-Editor/RszTool.App.SaveData.json'
    usage_path = Path(os.environ['LOCALAPPDATA']) / 'RszTool/uvar-flag-usage.json'
    settings = json.loads(settings_path.read_text(encoding='utf-8-sig'))
    uvar_path = Path(settings['UvarFilePath'])
    blob = uvar_path.read_bytes()
    groups = {}

    def q(offset):
        return struct.unpack_from('<Q', blob, offset)[0]

    def h(offset):
        return struct.unpack_from('<H', blob, offset)[0]

    def text(offset):
        end = offset
        while blob[end:end + 2] != b'\0\0':
            if end >= len(blob):
                raise ValueError('Unterminated UVAR string')
            end += 2
        return blob[offset:end].decode('utf-16-le')

    def read_group(base):
        if blob[base:base + 8] != b'\x03\0\0\0uvar':
            raise ValueError('Expected UVAR v3')
        name = text(base + q(base + 8))
        entries = {}
        for i in range(h(base + 44)):
            record = base + q(base + 16) + i * 48
            flag_name = text(base + q(record + 16))
            type_bits = struct.unpack_from('<I', blob, record + 40)[0]
            if type_bits & 0xffffff != 2 or type_bits & 0x40000000:
                continue
            match = re.fullmatch(re.escape(name) + r'_(\d+)', flag_name)
            if match:
                entries[int(match[1])] = str(uuid.UUID(bytes_le=blob[record:record + 16]))
        if name in groups:
            raise ValueError('Ambiguous UVAR group name: ' + name)
        groups[name] = entries
        for i in range(h(base + 46)):
            read_group(base + q(base + q(base + 24) + i * 8))

    read_group(0)
    montage = {}
    for line in (mod / 'id.txt').read_text(encoding='utf-8-sig').splitlines():
        match = re.fullmatch(r'([\d,\s]+)\s+(Knight|Chainsaw)', line.strip(), re.I)
        if not match:
            if line.strip():
                raise ValueError('Unrecognized id.txt line: ' + line)
            continue
        values = list(dict.fromkeys(int(x) for x in re.findall(r'\d+', match[1])))
        if any(not 0 <= x <= 0xffffffff for x in values):
            raise ValueError('MontageID exceeds UInt32')
        montage[match[2].lower()] = values
    if set(montage) != {'knight', 'chainsaw'}:
        raise ValueError('id.txt must contain Knight and Chainsaw')

    sections = {}
    section = None
    for raw in (mod / 'tot.txt').read_text(encoding='utf-8-sig').splitlines():
        line = raw.strip()
        if not line:
            continue
        if line.lower().startswith('chp'):
            section = line
            sections[section] = []
        else:
            match = re.fullmatch(r'(\d+)[wW]?', line)
            if not match or section is None:
                raise ValueError('Unrecognized tot.txt line: ' + line)
            sections[section].append(int(match[1]))
    mappings = {
        'chp0': 'Chapter0001', 'chp1': 'Chapter0101', 'chp2': 'Chapter0102',
        'chp3': 'Chapter0103', 'chp4': 'Chapter0201', 'chp5': 'Chapter0202',
        'chp6': 'Chapter0203', 'chp3-1': 'Chapter0301',
    }
    imports = []
    pending = []
    for section, values in sections.items():
        maximum = max(values)
        key = section.lower()
        group = 'Location55' if key.startswith('chpx-2') else args.chapter_x1 if key == 'chpx-1' else mappings.get(key)
        if group is None:
            pending.append({'section': section, 'maximum': maximum, 'reason': 'Needs UVAR group mapping'})
            continue
        if group not in groups:
            raise ValueError('Missing UVAR group: ' + group)
        missing = [i for i in range(maximum + 1) if i not in groups[group]]
        if missing:
            raise ValueError(f'{group} lacks existing flags {missing}; UVAR will not be changed')
        imports.append({'section': section, 'group': group, 'first': 0, 'maximum': maximum, 'count': maximum + 1})

    pools = {
        'chainsaw.character.ch1d6z0.MontageID': {'IncludeEnumValues': True, 'CustomIds': ','.join(map(str, montage['knight']))},
        'chainsaw.character.ch1d1z1.MontageID': {'IncludeEnumValues': True, 'CustomIds': ','.join(map(str, montage['chainsaw']))},
    }
    settings.setdefault('MontageIdPools', {}).update(pools)
    presets = settings.setdefault('MontageIdPresets', [])
    names = ['骑士（id.txt）', '电锯（id.txt）', '骑士和电锯（id.txt）']
    updates = [
        {'Name': names[0], 'Pools': {list(pools)[0]: list(pools.values())[0]}},
        {'Name': names[1], 'Pools': {list(pools)[1]: list(pools.values())[1]}},
        {'Name': names[2], 'Pools': pools},
    ]
    presets[:] = [preset for preset in presets if preset.get('Name') not in names] + updates
    settings['LastMontageIdPreset'] = names[2]

    usage = json.loads(usage_path.read_text(encoding='utf-8-sig')) if usage_path.exists() else {}
    prefix = str(uvar_path.resolve()).upper() + '|'
    for spec in imports:
        for number in range(spec['maximum'] + 1):
            key = prefix + groups[spec['group']][number]
            previous = usage.get(key, {})
            note = f"tot.txt：{spec['section']}，000～{spec['maximum']:03d} 连续已使用（按最大值）"
            existing_note = previous.get('Note', '')
            usage[key] = {'Used': True, 'Note': existing_note if note in existing_note else (existing_note + '\n' + note).strip()}

    timestamp = datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    backups = []

    def write_json(path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        backup = path.with_name(path.name + '.before-import-' + timestamp + '.bak')
        if path.exists():
            shutil.copy2(path, backup)
            backups.append(str(backup))
        temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
        try:
            with temporary.open('w', encoding='utf-8') as out:
                json.dump(data, out, ensure_ascii=False, separators=(',', ':'))
                out.flush()
                os.fsync(out.fileno())
            os.replace(temporary, path)
        finally:
            if temporary.exists():
                temporary.unlink()

    # All mappings and ranges have been resolved before the first settings write.
    write_json(settings_path, settings)
    write_json(usage_path, usage)
    result = {
        'settings': str(settings_path), 'usage': str(usage_path), 'uvar': str(uvar_path),
        'montage_ids': montage, 'presets': names, 'flag_ranges': imports,
        'marked_count': sum(x['count'] for x in imports), 'pending': pending, 'backups': backups,
    }
    report = workspace / 'Docs/PhantomRequiem-config-import.json'
    write_json(report, result)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
