"""Build the Space Engineers PB script and prototype blueprint from installed definitions."""
from pathlib import Path
import argparse
import itertools
import json
import subprocess
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
NAME = '[GG] Topdown Missile'
XSI = 'http://www.w3.org/2001/XMLSchema-instance'
ET.register_namespace('xsi', XSI)
DIR = {'Right': (1,0,0), 'Left': (-1,0,0), 'Up': (0,1,0),
       'Down': (0,-1,0), 'Backward': (0,0,1), 'Forward': (0,0,-1)}

def add(parent, tag, value=None, **attrs):
    el = ET.SubElement(parent, tag, {k: str(v) for k,v in attrs.items()})
    if value is not None: el.text = str(value)
    return el

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--game', type=Path, default=Path(r'Z:\SteamLibrary\steamapps\common\SpaceEngineers'))
    args = parser.parse_args()
    definitions = {}
    for path in (args.game/'Content'/'Data'/'CubeBlocks').glob('*.sbc'):
        for d in ET.parse(path).findall('.//Definition'):
            if d.findtext('CubeSize') == 'Small': definitions[d.findtext('Id/SubtypeId')] = d
    source = (ROOT/'src/missile/Program.cs').read_text(encoding='utf-8')
    body = source.split('// BEGIN PB BODY\n',1)[1].split('// END PB BODY',1)[0]
    dist = ROOT/'dist'
    (dist/'TopdownMissile.cs').write_bytes(body.encode('utf-8'))
    assert len(body.replace('\n','\r\n').encode('utf-16-le'))//2 < 100000
    with zipfile.ZipFile(dist/'TopdownMissile_Local.zip','w',zipfile.ZIP_DEFLATED) as z:
        entry = zipfile.ZipInfo('GroundGame Topdown/Script.cs',(2026,11,26,0,0,0))
        entry.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(entry,body.encode('utf-8'))

    root = ET.Element('Definitions')
    bp = add(add(root,'ShipBlueprints'),'ShipBlueprint')
    bp.set('{'+XSI+'}type','MyObjectBuilder_ShipBlueprintDefinition')
    add(bp,'Id',Type='MyObjectBuilder_ShipBlueprintDefinition',Subtype=NAME)
    add(bp,'DisplayName','GroundGame')
    add(bp,'Description','Planetary top-down missile prototype. Run TEST before LAUNCH. No live flight acceptance yet.')
    grid = add(add(bp,'CubeGrids'),'CubeGrid')
    add(grid,'EntityId',0)
    add(grid,'PersistentFlags','CastShadows InScene')
    pose = add(grid,'PositionAndOrientation')
    add(pose,'Position',x=0,y=0,z=0)
    add(pose,'Forward',x=0,y=0,z=-1)
    add(pose,'Up',x=0,y=1,z=0)
    add(grid,'GridSizeEnum','Small')
    blocks = add(grid,'CubeBlocks')
    occupied = {}
    manifest = []
    def block(sub, pos, name='', forward='Forward', up='Up', **fields):
        d = definitions[sub]
        typ = d.findtext('Id/TypeId')
        f, u = DIR[forward], DIR[up]
        assert sum(a*b for a,b in zip(f,u)) == 0
        # Grid Min is the axis-aligned occupied minimum, regardless of rotation.
        right = (f[1]*u[2]-f[2]*u[1], f[2]*u[0]-f[0]*u[2], f[0]*u[1]-f[1]*u[0])
        back = tuple(-v for v in f)
        size = tuple(int(d.find('Size').get(k)) for k in 'xyz')
        extent = tuple(sum(abs(axis[j])*n for axis,n in zip((right,u,back),size)) for j in range(3))
        cells = set(itertools.product(*(range(v,v+n) for v,n in zip(pos,extent))))
        assert not cells.intersection(occupied), ('overlap',sub,pos,cells.intersection(occupied))
        for c in cells: occupied[c] = sub
        b = add(blocks,'MyObjectBuilder_CubeBlock')
        b.set('{'+XSI+'}type','MyObjectBuilder_'+typ)
        add(b,'SubtypeName',sub)
        add(b,'Min',**dict(zip('xyz',pos)))
        add(b,'BlockOrientation',Forward=forward,Up=up)
        add(b,'ColorMaskHSV',x=0.58,y=-0.35,z=-0.2)
        add(b,'IntegrityPercent',1); add(b,'BuildPercent',1)
        if name: add(b,'CustomName','[GG-MISSILE] '+name)
        if typ not in ('CubeBlock','Warhead'): add(b,'Enabled','true')
        for k,v in fields.items(): add(b,k,v)
        manifest.append(dict(subtype=sub,position=pos,extent=extent,forward=forward,up=up,name=name))
        return b
    block('SmallHydrogenTank',(-2,3,-2),'Fuel A',FilledRatio=1,IsStockpiling='false')
    block('SmallHydrogenTank',(-2,8,-2),'Fuel B',FilledRatio=1,IsStockpiling='false')
    block('SmallBlockLargeHydrogenThrust',(-1,0,-1),'Main boost','Down','Backward',ThrustOverride=0)
    for p,f in [((3,5,0),'Right'),((-3,5,0),'Left'),((0,5,3),'Backward'),((0,5,-3),'Forward')]:
        block('SmallBlockSmallHydrogenThrust',p,'Lateral '+f,f,ThrustOverride=0)
    for x,z in [(3,0),(-3,0),(0,3),(0,-3)]:
        for y in range(10,13): block('SmallBlockConveyor',(x,y,z))
        block('SmallBlockSmallHydrogenThrust',(x,13,z),'Retro '+str((x,z)),'Up','Forward',ThrustOverride=0)
    # A connected deck supports the radar turret. Components mount to the tank.
    special = {(1,0),(-1,0),(0,1),(-2,-2),(-2,2),(2,-2),(2,2)}
    for x,z in itertools.product(range(-2,3),repeat=2):
        if (x,z) not in special: block('SmallBlockArmorBlock',(x,13,z))
    block('SmallProgrammableBlock',(1,13,0),'Computer','Down','Backward',Program=body,Storage='')
    block('SmallBlockGyro',(-1,13,0),'Gyro')
    block('SmallBlockRemoteControl',(0,13,1),'Remote','Up','Backward',DampenersEnabled='true',AutoPilotEnabled='false')
    for x,z in [(-2,-2),(-2,2),(2,-2),(2,2)]:
        block('SmallWarhead',(x,13,z),'Warhead',IsArmed='false',IsCountingDown='false',CountdownMs=10000)
    turret = block('SmallGatlingTurret',(-2,14,-2),'Radar',Range=600,EnableIdleRotation='true',IsShooting='false')
    flags = add(turret,'Flags')
    for key in ['Meteors','Missiles','Characters','Neutrals','Friends','SmallGrids','LargeGrids','Stations','Enemies']:
        add(flags,'Target'+key,str(key in ['SmallGrids','LargeGrids','Stations','Enemies']).lower())
    inventory = add(turret,'Inventory')
    item = add(add(inventory,'Items'),'MyObjectBuilder_InventoryItem')
    add(item,'Amount',20)
    content = add(item,'PhysicalContent'); content.set('{'+XSI+'}type','MyObjectBuilder_AmmoMagazine')
    add(content,'SubtypeName','NATO_25x184mm'); add(item,'ItemId',0)
    add(inventory,'nextItemId',1)
    block('SmallBlockBatteryBlock',(-1,8,3),'Battery',CurrentStoredPower=1.0,ChargeMode=0)
    for p,f,u in [((3,10,1),'Right','Up'),((-3,10,1),'Left','Up'),((1,10,-3),'Forward','Up'),
                  ((1,10,3),'Backward','Up'),((2,2,0),'Down','Backward')]:
        block('SmallCameraBlock',p,'Camera '+f,f,u)
    block('SmallBlockArmorBlock',(3,13,2))
    block('SmallCameraBlock',(3,14,2),'Camera nose','Up','Forward')
    block('SmallBlockArmorBlock',(3,13,-2))
    block('SmallBlockArmorBlock',(3,13,-3))
    block('SmallBlockRadioAntenna',(3,14,-3),'Antenna',BroadcastRadius=5000,EnableBroadcasting='true')
    add(grid,'DisplayName',NAME); add(grid,'IsStatic','false')
    add(grid,'DampenersEnabled','true')
    add(grid,'LinearVelocity',x=0,y=0,z=0); add(grid,'AngularVelocity',x=0,y=0,z=0)
    # Check occupied-cell connectivity. This and serializer acceptance cannot
    # establish physical mount strength or live conveyor behavior.
    reached = {next(iter(occupied))}; pending = list(reached)
    while pending:
        p = pending.pop()
        for d in DIR.values():
            q = tuple(a+b for a,b in zip(p,d))
            if q in occupied and q not in reached: reached.add(q); pending.append(q)
    assert len(reached) == len(occupied), 'Disconnected occupied hull'
    # Gas routes: main engine + lower lateral engines touch tank face ports;
    # each retro is fed by its own three-cube conveyor riser from a side port.
    for x,z in [(3,0),(-3,0),(0,3),(0,-3)]:
        assert all(occupied[(x,y,z)] == 'SmallBlockConveyor' for y in range(10,13))
    folder = ROOT/'blueprints'/NAME; folder.mkdir(exist_ok=True)
    ET.indent(root)
    ET.ElementTree(root).write(folder/'bp.sbc',encoding='utf-8',xml_declaration=True)
    with zipfile.ZipFile(dist/'GroundGame_Topdown_Prototype.zip','w',zipfile.ZIP_DEFLATED) as z:
        for path,name in [(folder/'bp.sbc','Blueprints/local/'+NAME+'/bp.sbc'),
                          (dist/'TopdownMissile.cs','IngameScripts/local/GroundGame Topdown/Script.cs'),
                          (ROOT/'docs/TOPDOWN-MISSILE.md','TOPDOWN-MISSILE.md')]:
            entry=zipfile.ZipInfo(name,(2026,11,26,0,0,0)); entry.compress_type=zipfile.ZIP_DEFLATED
            z.writestr(entry,path.read_bytes())
    out = ROOT/'output'/'missile'; out.mkdir(parents=True,exist_ok=True)
    (out/'blueprint-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    refs = ['Sandbox.Common','Sandbox.Game','SpaceEngineers.Game','VRage','VRage.Game','VRage.Library','VRage.Math','VRage.Scripting','netstandard']
    compiler = Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
    subprocess.run([str(compiler),'/nologo','/target:library','/out:'+str(out/'GroundGame.Topdown.dll')]+
        ['/reference:'+str(args.game/'Bin64'/(r+'.dll')) for r in refs]+[str(ROOT/'src/missile/Program.cs')],check=True)
    print('Built and compiled:',len(manifest),'blocks;',len(occupied),'occupied cells;',len(body),'PB characters')

if __name__ == '__main__': main()
