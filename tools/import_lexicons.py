"""Rebuild local lexical resources; preserves upstream files and source identifiers."""
import csv, json, re, html, hashlib
csv.field_size_limit(10_000_000)
from pathlib import Path
from html.parser import HTMLParser

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'biblioteca/lexicos'
OUT.mkdir(exist_ok=True)

class Plain(HTMLParser):
    def __init__(self):
        super().__init__(); self.parts = []
    def handle_starttag(self, tag, attrs):
        if tag in ('p','div','h1','br','hr'): self.parts.append('\n')
    def handle_endtag(self, tag):
        if tag in ('p','div','h1'): self.parts.append('\n')
    def handle_data(self, data): self.parts.append(data)
    def text(self): return re.sub(r'\n\s*\n+', '\n\n', ''.join(self.parts)).strip()

def write(name, title, attribution, license, url, revision, entries, **extra):
    data = dict(Id=name, Title=title, Attribution=attribution, License=license, SourceUrl=url,
                Revision=revision, Entries=entries, **extra)
    (OUT / (name+'.json')).write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')
    print(name, len(entries))

entries=[]
with (ROOT/'biblioteca/fontes/BDB/unabridged-BDB-Hebrew-lexicon.csv').open(encoding='utf-8-sig', newline='') as f:
    for row in csv.DictReader(f, delimiter='\t'):
        content=row['content']; plain=Plain(); plain.feed(content)
        lemma=re.search(r'<(?:bdbheb|bdbarc|heb)>(.*?)</(?:bdbheb|bdbarc|heb)>', content, re.S)
        codes=[c for c in re.findall(r'H\d+', row['StrongNumber']) if c!='H0']
        entries.append(dict(Id=row['BDBid'], Lemma=html.unescape(re.sub('<[^>]+>','',lemma[1])) if lemma else '',
                            Codes=codes, Definition=plain.text()))
write('BDB','BDB integral · inglês','Brown, Driver e Briggs; edição digital de Eliran Wong / Marvel Bible; dados de Tim Morton e John Isett. HTML convertido em texto; IDs e Strong preservados.',
      'Domínio público segundo o repositório. Formatação por Eliran Wong.',
      'https://github.com/eliranwong/unabridged-BDB-Hebrew-lexicon','6e8326c6884f8ef1399c7a365d660da9bb9e2d01',entries)

entries=[]
source=ROOT/'biblioteca/fontes/UBS/UBSHebrewDic-v0.9.4-pt.JSON'
for item in json.loads(source.read_text(encoding='utf-8-sig')):
    lines=[]
    for base in item.get('BaseForms') or []:
        lines += ['Forma / classe: '+', '.join(base.get('PartsOfSpeech') or [])]
        for meaning in base.get('LEXMeanings') or []:
            for sense in meaning.get('LEXSenses') or []:
                if sense.get('LanguageCode')!='pt': continue
                lines += ['; '.join(sense.get('Glosses') or [])]
                lines += [sense.get(k) or '' for k in ('DefinitionShort','DefinitionLong','Comments')]
                if sense.get('AIGenerated') or sense.get('HumanCheckRecommended'):
                    lines.append('Nota da fonte: tradução gerada por IA e/ou revisão humana recomendada.')
            domains=[d.get('Domain','') for d in meaning.get('LEXDomains') or []]
            if domains: lines.append('Domínios: '+', '.join(domains))
            refs=meaning.get('LEXReferences') or []
            if refs: lines.append('Referências da fonte (BBBCCCVVVSSWWW): '+', '.join(refs))
    codes=[]
    for code in item.get('StrongCodes') or []:
        if re.fullmatch(r'[HA]\d+',code): codes.append('H'+str(int(code[1:])))
    entries.append(dict(Id=item['MainId'],Lemma=item['Lemma'], Codes=list(dict.fromkeys(codes)),
                        Definition='\n\n'.join(s for s in lines if s.strip())))
write('UBS-PT','UBS · português','UBS Dictionary of Biblical Hebrew © United Bible Societies, 2023. Adaptado do Semantic Dictionary of Biblical Hebrew © 2000–2023 UBS. Edição portuguesa 0.9.4; campos lexicais extraídos para consulta, sem tradução automática adicional.',
      'CC BY-SA 4.0 · https://creativecommons.org/licenses/by-sa/4.0/',
      'https://github.com/ubsicap/ubs-open-license/tree/main/dictionaries/hebrew','a15a96cfaa4ca18a6c0339016645ba1e534db3c8',entries)

if __name__=='__main__':
    import sys
    if len(sys.argv)>1:
        from pypdf import PdfReader
        pdf=Path(sys.argv[1]).resolve(); entries=[]
        for i,page in enumerate(PdfReader(pdf).pages,1):
            text=page.extract_text() or ''
            entries.append(dict(Id=str(i),Lemma='Página '+str(i),Codes=[],Definition=text or 'Página sem texto pesquisável; consulte o PDF.'))
        write('HOLLADAY-LOCAL','Holladay · PDF pessoal','William L. Holladay · arquivo PDF fornecido pelo usuário. Consulta local por páginas; OCR não revisado. Sem vínculo automático com Strong ou morfologia.',
              'Arquivo pessoal; não incluído como base aberta nem autorizado para redistribuição.', '',hashlib.sha256(pdf.read_bytes()).hexdigest(),entries,LocalDocument=str(pdf))
