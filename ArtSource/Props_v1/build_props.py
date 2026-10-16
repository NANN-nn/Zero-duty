"""Build editable Zero Duty props with genuine Unity CCTV photographs. Run with Blender."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
random.seed(12)
bpy.ops.wm.read_factory_settings(use_empty=True)
assets = bpy.context.scene
assets.name = '01_ASSETS_real_scale'
assets.unit_settings.system = 'METRIC'
assets.unit_settings.scale_length = 1
current = None
root = None
groups = []

def material(name, color, metal=0, rough=.5, noise=0, emission=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    n=m.node_tree.nodes; p=n.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    if emission:
        p.inputs['Emission Color'].default_value=(*color,1); p.inputs['Emission Strength'].default_value=emission
    if noise:
        tex=n.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=150
        bump=n.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=noise; bump.inputs['Distance'].default_value=.0005
        m.node_tree.links.new(tex.outputs['Fac'],bump.inputs['Height']); m.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
    return m
navy=material('Paint | graphite navy',(.038,.063,.09),.55,.4,.15)
steel=material('Metal | brushed alloy',(.37,.42,.47),.85,.32,.12)
rubber=material('Rubber | charcoal',(.013,.018,.024),0,.8)
paper=material('Paper | warm archival',(.77,.72,.59),0,.85,.13)
ink=material('Ink | blue black',(.04,.07,.10),0,.8)
white=material('Print | warm white',(.76,.83,.86),0,.6)
cloth=material('Book | navy fabric',(.035,.062,.095),0,.8,.4)
yellow=material('Paper | muted yellow',(.76,.65,.35),0,.82,.12)
foil=material('Ration | silver foil',(.38,.43,.48),.82,.38,.5)
cookie=material('Biscuit | baked oat',(.48,.28,.10),0,.92,.65)
crumb=material('Biscuit | indent',(.15,.071,.025),0,.95)
cyan=material('Indicator | cyan',(.035,.55,.78),.2,.25,0,2)

def group(name):
    global current,root
    current=bpy.data.collections.new(name); assets.collection.children.link(current)
    root=bpy.data.objects.new(name,None); current.objects.link(root)
    root['asset_units']='meters'; root['purpose']='Editable source prop; no gameplay scripts'
    groups.append((current,root)); return root

def adopt(o,name,mat=None):
    o.name=name
    for c in list(o.users_collection): c.objects.unlink(o)
    current.objects.link(o); o.parent=root
    if mat: o.data.materials.append(mat)
    return o

def box(name,pos,size,mat,bevel=.002):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos); o=adopt(bpy.context.object,name,mat); o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Edge bevel (editable)','BEVEL'); mod.width=bevel; mod.segments=2
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o

def cyl(name,pos,r,depth,mat,rot=(0,0,0),vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=pos,rotation=rot)
    o=adopt(bpy.context.object,name,mat)
    mod=o.modifiers.new('Rim bevel','BEVEL');mod.width=min(r*.12,.0015);mod.segments=2
    for p in o.data.polygons:p.use_smooth=True
    o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o

def torus(name,pos,major,minor,mat,rot=(0,0,0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=8,location=pos,major_radius=major,minor_radius=minor,rotation=rot)
    return adopt(bpy.context.object,name,mat)

def text(body,pos,size,mat=white,rot=(0,0,0),align='CENTER'):
    d=bpy.data.curves.new(body,'FONT');d.body=body;d.size=size;d.align_x=align;d.align_y='CENTER';d.extrude=0
    o=bpy.data.objects.new(body,d);current.objects.link(o);o.parent=root;o.location=pos;o.rotation_euler=rot;d.materials.append(mat);return o

def mesh(name,verts,faces,mat):
    d=bpy.data.meshes.new(name);d.from_pydata(verts,[],faces);d.update();o=bpy.data.objects.new(name,d);current.objects.link(o);o.parent=root;d.materials.append(mat);return o

def screw(x,y,z):
    cyl('Captive screw',(x,y,z),.005,.003,steel,vertices=12)
    box('Screw slot',(x,y,z+.0017),(.006,.001,.0005),rubber,0)

for i in range(1,4):
    r=group(f'CAM_0{i}_WallReference')
    box('Steel backing',(0,0,.011),(.92,.565,.022),navy,.007)
    for x in [-.446,.446]: box('Side rail',(x,0,.028),(.029,.565,.018),navy,.003)
    for y in [-.267,.267]: box('Horizontal rail',(0,y,.028),(.875,.031,.018),navy,.003)
    box('Photo recess',(0,.017,.024),(.847,.480,.005),rubber,.002)
    im=bpy.data.images.load(str(OUT/'Textures'/f'CAM_0{i}_Normal.png')); im.pack()
    m=material(f'CAM_0{i} | actual Unity capture',(1,1,1),0,.88)
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im
    bs=m.node_tree.nodes.get('Principled BSDF');m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    bs.inputs['Specular IOR Level'].default_value=.12
    bpy.ops.mesh.primitive_plane_add(size=1,location=(0,.017,.027));p=adopt(bpy.context.object,'Actual camera photograph',m);p.scale=(.832,.468,1)
    for x in [-.444,.444]:
        for y in [-.265,.265]:screw(x,y,.039)
    for x in [-.27,.27]:torus('Rear hanging eye',(x,.16,-.005),.014,.003,steel)
    r['image_source']=f'Unity first / Monitor{i}; 1600x900; normal-state capture'

group('ShiftLog_Open')
for side in [-1,1]:
    x=side*.072
    box('Fabric cover',(x,0,.006),(.143,.205,.009),cloth,.004)
    box('Page block',(x,0,.016),(.134,.194,.013),paper,.002)
    for k in range(5):box('Page edge',(x,-.097,.012+k*.002),(.13,.0005,.0003),yellow,0)
    for k in range(12):box('Printed rule',(x,.048-k*.010,.023),(.112,.00045,.0002),ink,0)
    text('SHIFT LOG' if side<0 else 'OBSERVATIONS',(x,.077,.0235),.011,ink)
box('Book spine',(0,0,.009),(.008,.204,.015),cloth,.003)
box('Ribbon bookmark',(.047,-.09,.024),(.009,.071,.0008),navy,.0003)

group('ShiftLog_Closed')
box('Page block',(0,0,.015),(.132,.194,.024),paper,.002)
for z in [.002,.029]:box('Fabric cover',(0,0,z),(.143,.205,.006),cloth,.004)
box('Spine',(-.071,0,.016),(.008,.205,.03),cloth,.003)
text('ZERO DUTY',(0,.025,.0325),.016,white)
text('SHIFT LOG',(0,-.003,.0325),.011,white)
box('Cover stripe',(0,-.037,.0325),(.083,.004,.0004),steel,0)

group('InspectionClipboard')
box('Metal board',(0,0,.005),(.22,.30,.01),navy,.006)
box('Paper',(0,-.009,.011),(.19,.259,.001),paper,.001)
box('Spring clip',(0,.132,.017),(.08,.03,.009),steel,.004)
torus('Clip eye',(0,.155,.011),.012,.0025,steel)
text('STATION INSPECTION',(0,.10,.012),.012,ink)
for k in range(10):
    y=.068-k*.018
    box('Checklist box',(-.073,y,.012),(.006,.006,.0003),ink,0)
    box('Rule',(.009,y,.012),(.13,.0005,.0003),ink,0)
pen=cyl('Pencil',(.102,-.017,.018),.003,.20,navy,(math.pi/2,0,0),12)
cyl('Pencil ferrule',(.102,.074,.018),.0035,.012,steel,(math.pi/2,0,0),12)

group('StickyNotes_Stack')
box('Paper stack',(0,0,.009),(.08,.08,.018),yellow,.001)
for k in range(9):box('Sheet edge',(0,-.0401,.002+k*.0017),(.077,.0002,.0003),paper,0)
mesh('Lifted top note',[(-.04,-.04,.019),(.04,-.04,.022),(.04,.04,.019),(-.04,.04,.019)],[(0,1,2,3)],yellow)

group('FoldedPersonalNote')
o=mesh('Folded sheet',[(-.07,-.05,0),(0,-.05,.003),(.07,-.05,0),(-.07,.05,0),(0,.05,.003),(.07,.05,.008)],[(0,1,4,3),(1,2,5,4)],paper)
solid=o.modifiers.new('Paper thickness','SOLIDIFY');solid.thickness=.0004
text('FOR THE NEXT SHIFT',(0,.022,.005),.008,ink)
for k in range(4):box('Writing rule',(0,.007-k*.009,.004),(.10,.0005,.0002),ink,0)

group('CoffeeCan')
cyl('Aluminum body',(0,0,.061),.034,.114,steel)
cyl('Navy printed sleeve',(0,0,.058),.0344,.076,navy)
for z in [.006,.117]:torus('Rolled rim',(0,0,z),.0325,.002,steel)
cyl('Recessed lid',(0,0,.117),.0305,.0015,steel)
tab=torus('Pull tab',(0,-.005,.119),.008,.0016,steel);tab.scale=(.65,1,1)
text('COFFEE',(0,-.0348,.065),.011,white,(math.pi/2,0,0))
text('NIGHT SHIFT',(0,-.035,.048),.005,white,(math.pi/2,0,0))

group('RationPack')
box('Foil pouch',(0,0,.014),(.15,.095,.026),foil,.009)
for x in [-.081,.081]:
    box('Sealed end',(x,0,.012),(.018,.097,.006),foil,.001)
    for k in range(11):box('Crimp rib',(x,-.044+k*.008,.016),(.017,.0012,.001),steel,.0003)
box('Printed label',(0,0,.028),(.084,.068,.0007),navy,.001)
text('RATION',(0,.006,.029),.016)
text('STANDARD / 01',(0,-.014,.029),.006)

group('OatBiscuit')
box('Baked biscuit',(0,0,.007),(.095,.055,.014),cookie,.006)
for x in [-.03,-.01,.01,.03]:
    for y in [-.015,0,.015]:cyl('Baking perforation',(x,y,.014),.0021,.0005,crumb,vertices=10)
for i in range(32):
    x=random.uniform(-.04,.04);y=random.uniform(-.023,.023)
    cyl('Oat grain',(x,y,.0145),random.uniform(.0006,.0012),.0005,paper,vertices=6)

group('MaintenanceRobot_Mascot')
cyl('Display plinth',(0,0,.009),.075,.018,navy)
for x in [-.03,.03]:
    box('Foot',(x,-.008,.031),(.041,.054,.024),navy,.004)
    cyl('Knee joint',(x,0,.06),.013,.026,steel,(math.pi/2,0,0),16)
    box('Leg',(x,0,.072),(.023,.024,.036),steel,.003)
box('Torso',(0,0,.115),(.082,.052,.063),navy,.009)
box('Chest plate',(0,-.029,.117),(.061,.007,.036),steel,.003)
text('ZD-01',(0,-.033,.116),.009,ink,(math.pi/2,0,0))
cyl('Neck',(0,0,.157),.014,.017,steel)
box('Head',(0,0,.194),(.115,.066,.064),steel,.009)
box('Faceplate',(0,-.035,.194),(.083,.006,.045),navy,.006)
cyl('Eye housing',(-.013,-.043,.195),.021,.012,rubber,(math.pi/2,0,0))
cyl('Cyan eye',(-.013,-.050,.195),.015,.003,cyan,(math.pi/2,0,0))
for side in [-1,1]:
    cyl('Ear joint',(side*.060,0,.193),.018,.012,navy,(0,math.pi/2,0))
    cyl('Shoulder',(side*.051,0,.132),.015,.018,steel,(0,math.pi/2,0),16)
    arm=box('Upper arm',(side*.067,-.001,.114),(.022,.028,.043),navy,.004);arm.rotation_euler[1]=side*.3
    box('Gripper',(side*.075,-.004,.085),(.028,.032,.024),steel,.004)
    for delta in [-.01,.01]:box('Claw',(side*.075+delta,-.013,.068),(.007,.022,.02),navy,.002)
cyl('Antenna',( .039,.01,.244),.002,.050,steel,vertices=12)
cyl('Antenna tip',(.039,.01,.270),.005,.006,cyan,vertices=12)
for x in [-.043,.043]:
    for z in [.175,.213]:cyl('Head fastener',(x,-.034,z),.003,.002,navy,(math.pi/2,0,0),12)

# Sources remain at true meter scale, each in its own named collection.
for i,(col,r) in enumerate(groups):
    r.location=((i%4)*1.2,(i//4)*.9,0);col.instance_offset=r.location

preview=bpy.data.scenes.new('02_PRESENTATION')
preview.unit_settings.system='METRIC'
bpy.context.window.scene=preview
current=bpy.data.collections.new('Studio');preview.collection.children.link(current);root=None
# Gallery instances are display-scaled; the source meshes keep their true scale.
layout=[(-1.12,1.03,1),(0,1.03,1),(1.12,1.03,1),
        (-1.10,-.03,2.5),(-.56,-.03,1.65),(0,-.03,2.0),(.75,.12,2.5),(1.13,-.15,2.1),
        (-1.07,-1.03,3.3),(0,-1.0,3.6),(.52,-1.13,2.8),(1.15,-1.06,2.3)]
for (col,r),(x,y,s) in zip(groups,layout):
    o=bpy.data.objects.new(col.name+' [display]',None);preview.collection.objects.link(o)
    o.instance_type='COLLECTION';o.instance_collection=col;o.location=(x,y,.045);o.scale=(s,s,s)
floor=material('Studio | midnight',(.016,.026,.039),.1,.65)
box('Studio floor',(0,0,-.055),(200,200,.1),floor,.0)
text('ZERO DUTY / PROP COLLECTION',(0,1.63,.002),.10,white)
text('ACTUAL UNITY CCTV REFERENCES  /  NORMAL STATE',(0,.65,.002),.039,white)
text('SHIFT LOGS  /  INSPECTION  /  PERSONAL NOTES',(0,-.55,.002),.04,white)
text('COFFEE  /  RATION  /  MAINTENANCE MASCOT',(0,-1.60,.002),.04,white)
world=bpy.data.worlds.new('Studio World');preview.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.17,.23,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.32
for name,pos,power,size,color in [('Key',(-3,-2,6),550,5,(.84,.92,1)),('Warm rim',(3,2,4),420,4,(1,.84,.66)),('Fill',(0,4,6),450,4,(.68,.83,1))]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);preview.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,0))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('PresentationCamera');cam=bpy.data.objects.new('PresentationCamera',d);preview.collection.objects.link(cam)
cam.location=(0,-4.4,8.5);cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=4.05;preview.camera=cam
preview.render.engine='CYCLES';preview.cycles.samples=32;preview.cycles.use_denoising=True
preview.render.resolution_x=1800;preview.render.resolution_y=1800;preview.render.resolution_percentage=100
preview.view_settings.view_transform='AgX'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ZeroDuty_Props_v1.blend'))
preview.render.filepath=str(OUT/'Props_Preview.png');bpy.ops.render.render(write_still=True)
manifest=[]
for col,r in groups:
    manifest.append({'collection':col.name,'objects':len(col.objects),'vertices':sum(len(o.data.vertices) for o in col.objects if o.type=='MESH')})
(OUT/'build_complete.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('PROPS_BUILD_COMPLETE',len(groups))
