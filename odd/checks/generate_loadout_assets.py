"""Deterministic serialized Loadout assets. Run only when intentionally regenerating UI.
No Unity Editor process or runtime fallback is required to serialize this prefab.
"""
from pathlib import Path
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'Proyecto-Final'
ASSETS = PROJECT / 'Assets'

def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'magic-garden/daily-loadout/' + path).hex

def meta(path, importer='MonoImporter'):
    p = ASSETS / path
    m = Path(str(p) + '.meta')
    if not m.exists():
        m.write_text('fileFormatVersion: 2\nguid: ' + guid(path) + '\n' + importer + ':\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    return re.search(r'guid: ([a-f0-9]+)', m.read_text()).group(1)

controller_guid = meta('Scripts/Gameplay Systems/DailyLoadoutController.cs')
ui_guid = meta('Scripts/UI/DailyLoadoutUI.cs')
slot_guid = meta('Scripts/UI/LoadoutSlotView.cs')
meta('Scripts/Upgrades/Core/DailyLoadoutState.cs')
meta('Tests/Editor/Upgrades/DailyLoadoutTests.cs')
meta('Tests/Editor/Upgrades/DailyLoadoutIntegrationTests.cs')

IMAGE = 'fe87c0e1cc204ed48ad3b37840f39efc'
BUTTON = '4e29b1a8efbd4b44bb3f3716e73f07ff'
TMP = 'f4688fdb7df04437aeb418b961361dc5'
FONT = '8f586378b4e144a9851e7b34d9b748ee'
SCALER = '0cd44c1031e13a943bb63640046fad76'
RAYCASTER = 'dc42784cf147c0c48a680349fa168899'
blocks = []
nodes = []
next_id = 1000

def alloc():
    global next_id
    next_id += 1
    return next_id

def ref(i): return '{fileID: ' + str(i) + '}'
def common(go):
    return '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: ' + ref(go) + '\n'

def script(node, script_guid, fields=''):
    i = alloc()
    node['components'].append(i)
    blocks.append(f'--- !u!114 &{i}\nMonoBehaviour:\n' + common(node['go']) +
                  f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier:\n' + fields)
    return i

def node(name, parent=None, pos=(0,0), size=(100,100), anchor=(.5,.5), stretch=False, active=True):
    n = dict(go=alloc(), rt=alloc(), name=name, parent=parent, children=[], components=[], pos=pos, size=size,
             anchor=anchor, stretch=stretch, active=active)
    n['components'].append(n['rt'])
    if parent: parent['children'].append(n['rt'])
    nodes.append(n)
    return n

def renderer(n):
    i = alloc(); n['components'].append(i)
    blocks.append(f'--- !u!222 &{i}\nCanvasRenderer:\n' + common(n['go']) + '  m_CullTransparentMesh: 1\n')

def image(n, color=(.15,.18,.23,.98), raycast=False, filled=False):
    renderer(n)
    return script(n, IMAGE, '  m_Material: {fileID: 0}\n  m_Color: {r: %s, g: %s, b: %s, a: %s}\n' % color +
                  f'  m_RaycastTarget: {int(raycast)}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_Sprite: {{fileID: 0}}\n  m_Type: {3 if filled else 0}\n  m_PreserveAspect: 1\n  m_FillCenter: 1\n  m_FillMethod: 1\n  m_FillAmount: 0\n  m_FillClockwise: 1\n  m_FillOrigin: 0\n')

def text(parent, name, value, pos=(0,0), size=(100,30), font_size=18):
    n = node(name,parent,pos,size); renderer(n)
    i = script(n,TMP,'  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 0\n  m_Maskable: 1\n  m_text: '+json.dumps(value)+'\n' +
               f'  m_fontAsset: {{fileID: 11400000, guid: {FONT}, type: 2}}\n  m_sharedMaterial: {{fileID: 2180264, guid: {FONT}, type: 2}}\n  m_fontColor: {{r: 1, g: 1, b: 1, a: 1}}\n  m_fontSize: {font_size}\n  m_fontSizeBase: {font_size}\n  m_fontWeight: 400\n  m_enableAutoSizing: 1\n  m_fontSizeMin: 12\n  m_fontSizeMax: {font_size}\n  m_HorizontalAlignment: 2\n  m_VerticalAlignment: 512\n  m_enableWordWrapping: 1\n  m_overflowMode: 0\n  m_isRichText: 1\n  m_isOrthographic: 1\n')
    return i

def button(n, bg):
    return script(n,BUTTON,'  m_Navigation:\n    m_Mode: 0\n    m_WrapAround: 0\n    m_SelectOnUp: {fileID: 0}\n    m_SelectOnDown: {fileID: 0}\n    m_SelectOnLeft: {fileID: 0}\n    m_SelectOnRight: {fileID: 0}\n  m_Transition: 1\n  m_Colors:\n    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n    m_HighlightedColor: {r: 0.8, g: 1, b: 0.8, a: 1}\n    m_PressedColor: {r: 0.65, g: 0.8, b: 0.65, a: 1}\n    m_SelectedColor: {r: 1, g: 1, b: 1, a: 1}\n    m_DisabledColor: {r: 0.6, g: 0.6, b: 0.6, a: 1}\n    m_ColorMultiplier: 1\n    m_FadeDuration: 0.1\n  m_Interactable: 1\n  m_TargetGraphic: '+ref(bg)+'\n  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n')

def simple_button(parent,name,title,pos,size=(200,42)):
    n=node(name,parent,pos,size); bg=image(n,raycast=True); b=button(n,bg)
    text(n,'Label',title,size=(size[0]-12,size[1]-4))
    return b

def slot(parent,name,pos,size=(156,108),active=True):
    n=node(name,parent,pos,size,active=active); bg=image(n,raycast=True); b=button(n,bg)
    icon_node=node('Icon',n,(0,6),(44,44)); icon=image(icon_node,color=(1,1,1,1))
    cd_node=node('Cooldown',n,(0,6),(44,44)); cd=image(cd_node,color=(0,0,0,.7),filled=True)
    label=text(n,'Name','Empty',(0,-24),(size[0]-8,32),16)
    detail=text(n,'QuantityCooldown','',(0,-44),(size[0]-8,20),14)
    view=script(n,slot_guid, f'  button: {ref(b)}\n  icon: {ref(icon)}\n  cooldown: {ref(cd)}\n  background: {ref(bg)}\n  label: {ref(label)}\n  detail: {ref(detail)}\n')
    return n,view

root=node('Daily Loadout',size=(1920,1080),stretch=True)
canvas=alloc(); root['components'].append(canvas)
blocks.append(f'--- !u!223 &{canvas}\nCanvas:\n'+common(root['go'])+'  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_VertexColorAlwaysGammaSpace: 0\n  m_AdditionalShaderChannelsFlag: 25\n  m_SortingLayerID: 0\n  m_SortingOrder: 10\n  m_TargetDisplay: 0\n')
script(root,SCALER,'  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1920, y: 1080}\n  m_ScreenMatchMode: 0\n  m_MatchWidthOrHeight: 0.5\n  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n  m_DynamicPixelsPerUnit: 1\n')
script(root,RAYCASTER,'  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
hud=node('Unified Six Slot HUD',root,(0,100),(990,110),anchor=(.5,0))
hud_slots=[]
for i in range(6): hud_slots.append(slot(hud,f'Equipment {i+1}',((i-2.5)*164,0))[1])
open_button=simple_button(hud,'Open Loadout','Loadout [O]',(0,80))
panel=node('Loadout Configuration',root,stretch=True,active=False)
image(panel,color=(.02,.03,.05,.98),raycast=True)
text(panel,'Heading','DAILY LOADOUT',(0,455),(1100,60),36)
status=text(panel,'Daily Lock Status','DRAFT',(0,390),(1160,65),22)
text(panel,'Help','1–3: spells at night  |  4–6: plants by day  |  O: open/close  |  Esc: close\nClick an equipment slot, then choose an unlocked item below. Clear allows empty slots.',(0,305),(1200,72),20)
draft=[]
for i in range(6): draft.append(slot(panel,f'Draft {i+1}',((i-2.5)*180,190),size=(172,108))[1])
# Scrollable catalog: serialized viewport, grid and content fitting; rows grow at runtime.
scroll=node('Unlocked Items Scroll',panel,(0,-65),(1140,350)); image(scroll,color=(.1,.12,.16,1),raycast=True)
viewport=node('Viewport',scroll,stretch=True); script(viewport,'3312d7739989d2b4e91e6319e9a96d76','  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n')
content=node('Unlocked Items',viewport,(0,0),(1100,350),anchor=(.5,1))
content['pivot']=(.5,1)
script(content,'8a8695521f0d02e499659fee002a26c2','  m_Padding:\n    m_Left: 16\n    m_Right: 16\n    m_Top: 12\n    m_Bottom: 12\n  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n  m_CellSize: {x: 250, y: 108}\n  m_Spacing: {x: 18, y: 14}\n  m_Constraint: 1\n  m_ConstraintCount: 4\n')
script(content,'3245ec927659c4140ac4f8d17403cc18','  m_HorizontalFit: 0\n  m_VerticalFit: 2\n')
script(scroll,'1aa08ab6e0800fa44ae55d278d1423e3',f'  m_Content: {ref(content["rt"])}\n  m_Horizontal: 0\n  m_Vertical: 1\n  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n  m_ScrollSensitivity: 30\n  m_Viewport: {ref(viewport["rt"])}\n  m_HorizontalScrollbar: {{fileID: 0}}\n  m_VerticalScrollbar: {{fileID: 0}}\n  m_HorizontalScrollbarVisibility: 0\n  m_VerticalScrollbarVisibility: 0\n  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n')
_,template=slot(content,'Unlocked Item Template',(0,0),size=(250,108),active=False)
clear=simple_button(panel,'Clear Slot','Clear selected slot',(-350,-315),(260,48))
confirm=simple_button(panel,'Confirm Loadout','Confirm for today',(0,-315),(260,48))
close=simple_button(panel,'Close Loadout','Close [Esc]',(350,-315),(260,48))
fields=f'  loadout: {{fileID: 0}}\n  menu: {ref(panel["go"])}\n  hudSlots:\n' + ''.join('  - '+ref(i)+'\n' for i in hud_slots)
fields+='  draftSlots:\n'+''.join('  - '+ref(i)+'\n' for i in draft)
fields+=f'  status: {ref(status)}\n  openButton: {ref(open_button)}\n  closeButton: {ref(close)}\n  confirmButton: {ref(confirm)}\n  clearButton: {ref(clear)}\n  catalogRoot: {ref(content["rt"])}\n  catalogTemplate: {ref(template)}\n'
script(root,ui_guid,fields)

for n in nodes:
    blocks.append(f'--- !u!1 &{n["go"]}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n'+''.join('  - component: '+ref(i)+'\n' for i in n['components'])+f'  m_Layer: 5\n  m_Name: {n["name"]}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: {int(n["active"])}\n')
    a=(0,0) if n['stretch'] else n['anchor']; z=(1,1) if n['stretch'] else n['anchor']; size=(0,0) if n['stretch'] else n['size']; pivot=n.get('pivot',(.5,.5))
    children='  m_Children:\n'+''.join('  - '+ref(i)+'\n' for i in n['children']) if n['children'] else '  m_Children: []\n'
    blocks.append(f'--- !u!224 &{n["rt"]}\nRectTransform:\n'+common(n['go'])+'  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n'+children+'  m_Father: '+ref(n['parent']['rt'] if n['parent'] else 0)+'\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n'+f'  m_AnchorMin: {{x: {a[0]}, y: {a[1]}}}\n  m_AnchorMax: {{x: {z[0]}, y: {z[1]}}}\n  m_AnchoredPosition: {{x: {n["pos"][0]}, y: {n["pos"][1]}}}\n  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}\n  m_Pivot: {{x: {pivot[0]}, y: {pivot[1]}}}\n')

prefab='Prefabs/UI/DailyLoadout.prefab'
(ASSETS/prefab).write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(blocks),encoding='utf-8')
prefab_guid=meta(prefab,'PrefabImporter')

# Add the authority to the existing run-owned manager, retaining its day/flow references.
manager=ASSETS/'Prefabs/Managers/LevelManager.prefab'
s=manager.read_text(encoding='utf-8')
component=6789012345678901234
if controller_guid not in s:
    s=s.replace('  - component: {fileID: 5678901234567890123}\n','  - component: {fileID: 5678901234567890123}\n  - component: '+ref(component)+'\n',1)
    s+=f'\n--- !u!114 &{component}\nMonoBehaviour:\n'+common(993218346133697943)+f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {controller_guid}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier:\n  spells: {{fileID: 0}}\n  seeds: {{fileID: 0}}\n  unlocks: {{fileID: 0}}\n  days: {{fileID: 2345678901234567890}}\n  flow: {{fileID: 1234567890123456789}}\n  player: {{fileID: 0}}\n  pause: {{fileID: 5678901234567890123}}\n'
    manager.write_text(s,encoding='utf-8',newline='')

# Retire legacy gameplay bars at the prefab level. UIManager no longer drives them.
for old in ['HUD Seed Slots.prefab','HUD Spell Slots.prefab']:
    p=ASSETS/'Prefabs/UI/Canvas/Game/HUD'/old
    s=p.read_text(encoding='utf-8')
    s=s.replace('  m_IsActive: 1','  m_IsActive: 0',1)
    script_id='6c8fd0b6989339e4296c5c28ecf71587' if 'Seed' in old else '29839c071da47ba429444c8b0f64069d'
    s=re.sub(r'(  m_Enabled: )1(\n  m_EditorHideFlags: 0\n  m_Script: \{fileID: 11500000, guid: '+script_id+r')',r'\g<1>0\2',s,count=1)
    p.write_text(s,encoding='utf-8',newline='')

scene=ASSETS/'Scenes/Game.unity'; s=scene.read_text(encoding='utf-8')
instance=8100000000000000001; transform=8100000000000000002
if prefab_guid not in s:
    added=f'''--- !u!1001 &{instance}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications: []
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {prefab_guid}, type: 3}}
--- !u!224 &{transform} stripped
RectTransform:
  m_CorrespondingSourceObject: {{fileID: {root['rt']}, guid: {prefab_guid}, type: 3}}
  m_PrefabInstance: {{fileID: {instance}}}
  m_PrefabAsset: {{fileID: 0}}
'''
    s=s.replace('--- !u!1660057539 &9223372036854775807\n',added+'--- !u!1660057539 &9223372036854775807\n')
    s+='  - '+ref(transform)+'\n'
    scene.write_text(s,encoding='utf-8',newline='')
print('Serialized Loadout prefab, manager authority, legacy bars and Game scene integration generated.')
