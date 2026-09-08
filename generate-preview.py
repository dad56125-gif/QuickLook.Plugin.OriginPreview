"""Render a copy of an OPJU through a private Origin session; never save the source."""
import argparse, hashlib, json, os, pathlib, shutil, tempfile, time, uuid
import xml.etree.ElementTree as ET
from PIL import Image

def main():
    p=argparse.ArgumentParser()
    p.add_argument('source'); p.add_argument('--width',type=int,default=1000)
    p.add_argument('--force',action='store_true')
    args=p.parse_args()
    source=pathlib.Path(args.source).resolve()
    if source.suffix.lower()!='.opju': raise ValueError('Expected .opju')
    data=source.read_bytes(); key=hashlib.sha256(data).hexdigest()
    cache=pathlib.Path(os.environ['LOCALAPPDATA'])/'QuickLook'/'OriginPreview'/'cache'/key
    cache.mkdir(parents=True,exist_ok=True)
    args.width=max(200,min(args.width,1000))
    if (cache/'manifest.xml').exists() and not args.force:
        try:
            if ET.parse(cache/'manifest.xml').getroot().get('maxEdge')==str(args.width) and ET.parse(cache/'manifest.xml').getroot().get('graphPolicy')=='standalone-v1': return
        except (ET.ParseError,OSError): pass
    # Cross-process lock to prevent duplicate private Origin sessions for one file.
    import msvcrt
    lock=open(cache/'render.lock','a+b'); lock.seek(0); lock.write(b'0'); lock.flush(); lock.seek(0)
    deadline=time.monotonic()+150
    while True:
        try:
            lock.seek(0); msvcrt.locking(lock.fileno(),msvcrt.LK_NBLCK,1); break
        except OSError:
            if time.monotonic()>deadline: raise TimeoutError('Preview generation is still running')
            time.sleep(.2)
    if not args.force and (cache/'manifest.xml').exists():
        try:
            if ET.parse(cache/'manifest.xml').getroot().get('maxEdge')==str(args.width) and ET.parse(cache/'manifest.xml').getroot().get('graphPolicy')=='standalone-v1': return
        except (ET.ParseError,OSError): pass
    # Serialize rendering across projects to avoid opening many Origin sessions.
    global_lock=open(cache.parent/'renderer.lock','a+b'); global_lock.seek(0); global_lock.write(b'0'); global_lock.flush()
    deadline=time.monotonic()+150
    while True:
        try:
            global_lock.seek(0); msvcrt.locking(global_lock.fileno(),msvcrt.LK_NBLCK,1); break
        except OSError:
            if time.monotonic()>deadline: raise TimeoutError('Origin preview renderer is busy')
            time.sleep(.2)
    import OriginExt as O
    started=time.monotonic()
    batch=uuid.uuid4().hex[:8]
    with tempfile.TemporaryDirectory(prefix='origin-preview-',ignore_cleanup_errors=True) as temp:
        copy=pathlib.Path(temp)/'preview-source.opju'; copy.write_bytes(data)
        app=O.Application()
        try:
            app.Visible=0
            if not app.Load(str(copy)): raise RuntimeError('Origin could not load the project copy')
            app.LT_execute('sec -poc;')
            active=app.ActivePage.GetName() if app.ActivePage else ''
            pages=[g for g in app.GraphPages if g.GetNumProp('isEmbedded') == 0]
            pages.sort(key=lambda g: g.GetName()!=active)
            manifest=ET.Element('Preview',sha256=key,maxEdge=str(args.width),graphPolicy='standalone-v1',source=str(source),complete='false')
            for i,page in enumerate(pages):
                name='graph-%s-%03d.png'%(batch,i)
                command='expG2img type:=png name:="%s" width:=%d path:="%s";'%(name[:-4],args.width,str(temp))
                app.LT_execute('win -a %s;'%page.GetName())
                if not app.LT_execute(command): raise RuntimeError('Origin export command failed: '+page.GetName())
                target=pathlib.Path(temp)/name
                if not target.exists() or target.stat().st_size==0: raise RuntimeError('Export failed: '+page.GetName())
                with Image.open(target) as image:
                    image.thumbnail((args.width,args.width),Image.Resampling.LANCZOS)
                    image.save(cache/name,format='PNG')
                ET.SubElement(manifest,'Page',file=name,name=page.GetName())
                if i==0: first_seconds=round(time.monotonic()-started,3)
            if not pages: raise RuntimeError('No graph pages found')
            manifest.set('complete','true')
            ET.ElementTree(manifest).write(cache/'manifest.tmp',encoding='utf-8',xml_declaration=True)
            os.replace(cache/'manifest.tmp',cache/'manifest.xml')
            (cache/'render.json').write_text(json.dumps({'seconds':round(time.monotonic()-started,2),'first_image_seconds':first_seconds,'graphs':len(pages),'maxEdge':args.width,'method':'expG2img'},indent=2))
            keep={el.get('file') for el in manifest}
            for old in cache.glob('graph-*.png'):
                if old.name not in keep and old.parent.resolve()==cache.resolve(): old.unlink()
        finally:
            page=None
            pages=None
            try:
                app.NewProject()
            finally:
                app.Exit()
    lock.close()

if __name__=='__main__':
    main()

