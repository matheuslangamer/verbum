from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as E
import json
from pypdf import PdfReader

root=Path(__file__).resolve().parents[1]
source=Path('C:/Users/mathe/OneDrive/2026/SEMIÁRIO/4 semestre/HEBRAICO IV')
out=root/'tmp/hebraico-iv'
out.mkdir(parents=True,exist_ok=True)
files=list(source.glob('*.docx'))+[source/'PROVA 2/ESTUDO PROVA 1.pdf',source/'PROVA 2/Verbo_Hebraico_Identificacao.pdf']
ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
records=[]
for path in files:
    if path.suffix=='.docx':
        with ZipFile(path) as z:
            doc=E.fromstring(z.read('word/document.xml'))
            rows=[]
            for element in doc.find('w:body',ns):
                if element.tag.endswith('}p'):
                    text=''.join(element.itertext()) if False else ''.join(t.text or '' for t in element.findall('.//w:t',ns))
                    if text.strip(): rows.append(text)
                elif element.tag.endswith('}tbl'):
                    for row in element.findall('w:tr',ns):
                        rows.append(' | '.join(''.join(t.text or '' for t in cell.findall('.//w:t',ns)) for cell in row.findall('w:tc',ns)))
            text='\n'.join(rows)
            images=[p for p in z.namelist() if p.startswith('word/media/')]
            pages=None
    else:
        pdf=PdfReader(path); pages=len(pdf.pages); images=[]
        text='\n'.join(f'\n[Página {i+1}]\n'+(p.extract_text() or '') for i,p in enumerate(pdf.pages))
    (out/(path.stem+'.txt')).write_text(text,encoding='utf-8')
    records.append(dict(path=str(path),characters=len(text),pages=pages,images=images))
(out/'inventory.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
for r in records: print(Path(r['path']).name,r['characters'],'chars',r['pages'],'pages',len(r['images']),'images')
