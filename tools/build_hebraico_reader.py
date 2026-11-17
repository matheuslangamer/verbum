"""Build the study EPUB and local library records from the user's supplied files."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from html import escape
import re, json, hashlib, shutil
import xml.etree.ElementTree as E
from pypdf import PdfReader

root=Path(__file__).resolve().parents[1]
library=root/'biblioteca/livros'
originals=library/'originais'
originals.mkdir(parents=True,exist_ok=True)
source_root=Path('C:/Users/mathe/OneDrive/2026/SEMIÁRIO/4 semestre/HEBRAICO IV')
figure_directory=library/'imagens/hebraico-iv'
figure_directory.mkdir(parents=True,exist_ok=True)
for source,member,name in [('aula 04.docx','word/media/image1.png','joel-2-parte-1.png'),('aula 04.docx','word/media/image2.png','joel-2-parte-2.png'),('aula 07.docx','word/media/image1.png','joel-3.png')]:
    with ZipFile(source_root/source) as z: (figure_directory/name).write_bytes(z.read(member))
markdown=(root/'biblioteca/apostila-hebraico-iv.md').read_text(encoding='utf-8')
parts=re.split(r'^## ',markdown,flags=re.M)[1:]
chapters=[]
def blocks(lines):
    result=[]; i=0
    while i<len(lines):
        line=lines[i].strip()
        if not line: i+=1; continue
        image=re.fullmatch(r'!\[(.*?)\]\((.*?)\)',line)
        if image:
            result.append(dict(Kind='image',Text=image[1],Image=image[2],Rows=[])); i+=1; continue
        if line.startswith('|'):
            rows=[]
            while i<len(lines) and lines[i].strip().startswith('|'):
                row=[c.strip() for c in lines[i].strip().strip('|').split('|')]
                if not all(re.fullmatch(r':?-+:?',c) for c in row): rows.append(row)
                i+=1
            result.append(dict(Kind='table',Text='',Rows=rows)); continue
        result.append(dict(Kind='paragraph',Text=line,Rows=[])); i+=1
    return result
for part in parts:
    title,body=part.split('\n',1)
    chapters.append(dict(Title=title.strip(),Blocks=blocks(body.splitlines())))
def html_blocks(items):
    values=[]
    for item in items:
        if item['Kind']=='image':
            values.append('<p><img src="'+escape(item['Image'],quote=True)+'" alt="'+escape(item['Text'],quote=True)+'" style="max-width:100%"/></p><p>'+escape(item['Text'])+'</p>')
        elif item['Kind']=='table':
            values.append('<table>'+''.join('<tr>'+''.join(('<th>' if i==0 else '<td>')+escape(c)+('</th>' if i==0 else '</td>') for c in row)+'</tr>' for i,row in enumerate(item['Rows']))+'</table>')
        else: values.append('<p>'+escape(item['Text'])+'</p>')
    return ''.join(values)
epub=library/'Hebraico IV — apostila de estudo.epub'
css='body{font-family:serif;line-height:1.65;margin:5%;color:#24332f}h1{color:#386754;line-height:1.2}p{margin:0 0 1em}table{border-collapse:collapse;width:100%;font-size:.9em;margin:1em 0}td,th{border:1px solid #dce2d8;padding:.55em;text-align:start;vertical-align:top}th{background:#edf1e9}a{color:#386754}'
with ZipFile(epub,'w') as z:
    z.writestr('mimetype','application/epub+zip')
    z.writestr('META-INF/container.xml','<?xml version="1.0"?><container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles></container>')
    z.writestr('OEBPS/style.css',css,compress_type=ZIP_DEFLATED)
    for figure in figure_directory.glob('*.png'): z.write(figure,'OEBPS/imagens/hebraico-iv/'+figure.name,compress_type=ZIP_DEFLATED)
    manifest=['<item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>','<item id="css" href="style.css" media-type="text/css"/>']+[f'<item id="fig{i}" href="imagens/hebraico-iv/{figure.name}" media-type="image/png"/>' for i,figure in enumerate(figure_directory.glob('*.png'))]; spine=[]; nav=[]
    for i,chapter in enumerate(chapters,1):
        name=f'chapter{i:02}.xhtml'; manifest.append(f'<item id="c{i}" href="{name}" media-type="application/xhtml+xml"/>'); spine.append(f'<itemref idref="c{i}"/>'); nav.append(f'<li><a href="{name}">{escape(chapter["Title"])}</a></li>')
        z.writestr('OEBPS/'+name,'<?xml version="1.0" encoding="utf-8"?><html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR"><head><title>'+escape(chapter['Title'])+'</title><link rel="stylesheet" href="style.css"/></head><body><h1>'+escape(chapter['Title'])+'</h1>'+html_blocks(chapter['Blocks'])+'</body></html>',compress_type=ZIP_DEFLATED)
    z.writestr('OEBPS/nav.xhtml','<?xml version="1.0" encoding="utf-8"?><html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Sumário</title></head><body><nav epub:type="toc"><h1>Hebraico IV</h1><ol>'+''.join(nav)+'</ol></nav></body></html>')
    z.writestr('OEBPS/content.opf','<?xml version="1.0" encoding="utf-8"?><package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="book">verbum-hebraico-iv-2026</dc:identifier><dc:title>Hebraico IV — apostila de estudo</dc:title><dc:language>pt-BR</dc:language><dc:creator>Organização de estudo pessoal no VERBUM</dc:creator><meta property="dcterms:modified">2026-11-13T00:00:00Z</meta></metadata><manifest>'+''.join(manifest)+'</manifest><spine>'+''.join(spine)+'</spine></package>')

def save(path,sections,title=None,attribution='Arquivo pessoal fornecido para leitura e estudo. Original preservado. Texto extraído; confira no original a diagramação e possíveis falhas de OCR.',identifier=None):
    data=path.read_bytes(); code=hashlib.sha256(data).hexdigest(); target=originals/(code+path.suffix.lower()); shutil.copy2(path,target)
    doc=dict(Id=identifier or code,Title=title or path.stem,Format=path.suffix[1:].upper(),Original='originais/'+target.name,Attribution=attribution,Sections=sections)
    (library/(doc['Id']+'.json')).write_text(json.dumps(doc,ensure_ascii=False),encoding='utf-8')
    return dict(id=doc['Id'],title=doc['Title'],sections=len(sections),characters=sum(len(b.get('Text',''))+sum(sum(map(len,row)) for row in b.get('Rows',[])) for s in sections for b in s['Blocks']))

inventory=json.loads((root/'tmp/hebraico-iv/inventory.json').read_text(encoding='utf-8'))
report=[]; ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
for item in inventory:
    path=Path(item['path'])
    if path.name.startswith('preparação'): continue
    if path.suffix=='.pdf':
        sections=[dict(Title=f'Página {i+1}',Blocks=[dict(Kind='paragraph',Text=(p.extract_text() or '').replace('\0',''),Rows=[])]) for i,p in enumerate(PdfReader(path).pages)]
    else:
        with ZipFile(path) as z:
            xml=E.fromstring(z.read('word/document.xml')); content=[]
            relationships={r.attrib['Id']:r.attrib.get('Target','') for r in E.fromstring(z.read('word/_rels/document.xml.rels')) if r.attrib.get('TargetMode')!='External'}
            for e in xml.find('w:body',ns):
                if e.tag.endswith('}p'):
                    text=''.join(t.text or '' for t in e.findall('.//w:t',ns))
                    if text.strip(): content.append(dict(Kind='paragraph',Text=text,Rows=[]))
                    for blip in e.iter('{http://schemas.openxmlformats.org/drawingml/2006/main}blip'):
                        rel=blip.attrib.get('{http://schemas.openxmlformats.org/officeDocument/2006/relationships}embed',''); target=relationships.get(rel,'')
                        import posixpath
                        member=posixpath.normpath('word/'+target)
                        if member not in z.namelist(): continue
                        code=hashlib.sha256(path.read_bytes()).hexdigest(); filename=hashlib.sha256(member.encode()).hexdigest()+Path(member).suffix
                        relative=Path('imagens')/code/filename; destination=library/relative; destination.parent.mkdir(parents=True,exist_ok=True); destination.write_bytes(z.read(member))
                        content.append(dict(Kind='image',Text='Ilustração da aula original',Image=relative.as_posix(),Rows=[]))
                elif e.tag.endswith('}tbl'):
                    content.append(dict(Kind='table',Text='',Rows=[[''.join(t.text or '' for t in c.findall('.//w:t',ns)) for c in r.findall('w:tc',ns)] for r in e.findall('w:tr',ns)]))
            sections=[dict(Title=path.stem,Blocks=content)]
    report.append(save(path,sections))
report.append(save(epub,chapters,'Hebraico IV — apostila de estudo','Síntese didática de estudo pessoal organizada a partir das 10 aulas, dos 2 PDFs e das tabelas fornecidas. Inclui exemplos, 16 exercícios, respostas comentadas e mapa de fontes. Não é gabarito oficial do curso.',identifier='hebraico-iv-apostila'))
(root/'docs/document-library-import.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Títulos',len(report),'Capítulos da apostila',len(chapters),'Palavras aproximadas',len(markdown.split()))
print('EPUB',epub)
