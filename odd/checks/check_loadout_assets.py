"""Dependency-free structural checks; these do not replace Unity deserialization."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
A = ROOT / 'Proyecto-Final/Assets'
checks = 0

def check(condition, message):
    global checks
    checks += 1
    if not condition: raise AssertionError(message)

def guid(path):
    return re.search(r'^guid: ([a-f0-9]{32})$', Path(str(A / path) + '.meta').read_text(), re.M).group(1)

def records(text):
    result = {}
    for match in re.finditer(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S):
        id = int(match[2])
        check(id not in result, 'Duplicate local object ID: ' + str(id))
        result[id] = (int(match[1]), match[3])
    return result

prefab_path = 'Prefabs/UI/DailyLoadout.prefab'
prefab = (A / prefab_path).read_text()
objects = records(prefab)
for id, (_, data) in objects.items():
    for target in re.findall(r'\{fileID: (\d+)\}', data):
        check(int(target) == 0 or int(target) in objects, f'Broken local reference {target} on {id}')
ui_guid = guid('Scripts/UI/DailyLoadoutUI.cs')
slot_guid = guid('Scripts/UI/LoadoutSlotView.cs')
uis = [d for _, d in objects.values() if 'guid: ' + ui_guid in d]
check(len(uis) == 1, 'Exactly one unified presenter')
ui = uis[0]
for array in ('hudSlots', 'draftSlots'):
    ids = re.search(r'^  ' + array + r':\n((?:  - \{fileID: \d+\}\n)+)', ui, re.M)[1]
    ids = [int(i) for i in re.findall(r'fileID: (\d+)', ids)]
    check(len(ids) == len(set(ids)) == 6, array + ' has six unique slots')
    for id in ids:
        check('guid: ' + slot_guid in objects[id][1], 'Slot points to the serialized slot presenter')
        for field in ('button', 'icon', 'cooldown', 'background', 'label', 'detail'):
            check(bool(re.search(r'^  ' + field + r': \{fileID: [1-9]\d*\}', objects[id][1], re.M)), 'Missing slot field: ' + field)
for field in ('menu', 'status', 'openButton', 'closeButton', 'confirmButton', 'clearButton', 'catalogRoot', 'catalogTemplate'):
    check(bool(re.search(r'^  '+field+r': \{fileID: [1-9]\d*\}', ui, re.M)), 'Missing UI reference: ' + field)
for id, (kind, data) in objects.items():
    if kind != 224: continue
    father = int(re.search(r'm_Father: \{fileID: (\d+)\}', data)[1])
    if father:
        check(father in objects and objects[father][0] == 224, 'RectTransform parent exists')
        check('  - {fileID: '+str(id)+'}' in objects[father][1], 'Parent lists child')
check('m_Name: Loadout Configuration' in prefab and 'm_Name: Unlocked Items Scroll' in prefab, 'Serialized menu and scrollable catalog')
check('guid: 1aa08ab6e0800fa44ae55d278d1423e3' in prefab, 'Scrollable item list is wired')
scene = (A / 'Scenes/Game.unity').read_text()
pg = guid(prefab_path)
check(scene.count('m_SourcePrefab: {fileID: 100100000, guid: '+pg) == 1, 'Game instantiates exactly one Loadout UI prefab')
check('  - {fileID: 8100000000000000002}' in scene.split('SceneRoots:')[1], 'UI instance is registered as a scene root')
manager = (A / 'Prefabs/Managers/LevelManager.prefab').read_text()
check('guid: '+guid('Scripts/Gameplay Systems/DailyLoadoutController.cs') in manager, 'Run-owned manager has equipment authority')
for field in ('days', 'flow', 'pause'):
    check(bool(re.search(r'^  '+field+r': \{fileID: [1-9]\d*\}', manager, re.M)), 'Manager reference: '+field)
for name in ('Seed', 'Spell'):
    old = (A / f'Prefabs/UI/Canvas/Game/HUD/HUD {name} Slots.prefab').read_text()
    check('  m_IsActive: 0' in old.split('--- !u!224')[0], 'Retired '+name+' bar is inactive')
    check('  m_Enabled: 0\n  m_EditorHideFlags: 0\n  m_Script:' in old, 'Retired input presenter disabled')
for path in ('Scripts/Gameplay Systems/DailyLoadoutController.cs', 'Scripts/UI/DailyLoadoutUI.cs',
             'Scripts/UI/LoadoutSlotView.cs', 'Scripts/Upgrades/Core/DailyLoadoutState.cs',
             'Tests/Editor/Upgrades/DailyLoadoutTests.cs', 'Tests/Editor/Upgrades/DailyLoadoutIntegrationTests.cs'):
    check((A / path).exists() and bool(guid(path)), 'New script and stable metadata: '+path)
# Existing tutorial objective SeedTooltipDisplayed must remain reachable after retiring the old HUD.
slot_source = (A / 'Scripts/UI/LoadoutSlotView.cs').read_text()
presenter_source = (A / 'Scripts/UI/DailyLoadoutUI.cs').read_text()
check('IPointerEnterHandler' in slot_source and 'IPointerExitHandler' in slot_source,
      'Unified slots must provide custom tooltip pointer entry/exit (existing tutorial objective 23)')
check('hudSlots[i].Hovered +=' in presenter_source and 'hudSlots[i].Unhovered +=' in presenter_source,
      'All unified HUD slots must wire tooltip entry and exit')
check('UIManager.Instance?.ShowTooltipForSlot(loadout.FindSeed(id))' in presenter_source,
      'Plant hover must resolve stable identity to current seed storage before forwarding existing tooltip')
check('UIEvents.RequestSpellTooltip' in presenter_source and 'UIEvents.HideSpellTooltip' in presenter_source,
      'Existing spell tooltip forwarding must remain available')
print(f'PASS: {checks} serialized Loadout references and structural assertions (not Unity asset import).')
