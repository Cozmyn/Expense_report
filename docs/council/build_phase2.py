"""Create a design routing matrix without changing implementation acceptance statuses."""
from pathlib import Path
import json,csv,collections
p=Path(__file__).parent
source=json.loads((p/'initial-matrix.json').read_text())
def route(r):
 s=(r['source_id']+' '+r['source_section']+' '+r['requirement']).lower()
 if any(k in s for k in ['ocr','extraction','preview','document ai']):return 'B4','6','OCR Engineer + Designer','Real-document extraction, geometry, correction and preview recovery'
 if any(k in s for k in ['erp','tbr-01','fr-vat','br-011','tax','vat','tva','fx','posting']):return 'B1','5, 9','Architect + Compliance + QA','Financial boundary, effective version and exact export acceptance'
 if any(k in s for k in ['audit','retention','master','nomencl','import','legal hold','merge']):return 'B6','8, 9','Back Office + Compliance','Transactional integrity, historical preservation and complete scoped export'
 if any(k in s for k in ['auth','password','login','language','localiz','accessib','profile','permission','role','design','glossary']):return 'B2','2, 3, 4, 10','Designer + Content + Architect','RO/EN, keyboard/accessibility and positive/negative permission coverage'
 if any(k in s for k in ['manager','finance','card','reporting','notification','analytics','approval','workflow']):return 'B5','7, 8','Back Office + QA','Role/state journeys, concurrency and recovery'
 if any(k in s for k in ['employee','receipt','attachment','expense line','front','field','form','bon','draft','submit']):return 'B3','5','Front Office + QA','Conditional fields, evidence coverage and autosave/conflict recovery'
 return 'B7','11','Product Owner + QA','Individual source acceptance review; refine compound cases before sign-off'
fields=list(source[0])+['design_batch','design_sections','design_owner','design_gate','design_assessment']
with (p/'PHASE-2-MATRIX.csv').open('w',newline='',encoding='utf-8-sig') as f:
 w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
 for r in source:
  batch,section,owner,gate=route(r)
  w.writerow({**r,'evidence':json.dumps(r['evidence'],ensure_ascii=False),'design_batch':batch,'design_sections':section,'design_owner':owner,'design_gate':gate,'design_assessment':'Provisional workstream routing; does not prove individual semantic coverage or implemented behavior.'})
with (p/'PHASE-2-MATRIX.csv').open(encoding='utf-8-sig') as f:rows=list(csv.DictReader(f))
assert len(rows)==len(source)==len({r['id'] for r in rows})
assert all(a['id']==b['id'] and a['status']==b['status'] and a['requirement']==b['requirement'] for a,b in zip(source,rows))
# WCAG relative luminance of selected foreground/background token pairs.
def lum(h):
 rgb=[int(h[i:i+2],16)/255 for i in (0,2,4)];v=[c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in rgb];return sum(a*b for a,b in zip(v,[.2126,.7152,.0722]))
pairs=[('body','172B3A','FFFFFF',4.5),('muted','526171','FFFFFF',4.5),('primary button','FFFFFF','075E50',4.5),('success','166534','F0FDF4',4.5),('warning','854D0E','FEFCE8',4.5),('error','B91C1C','FEF2F2',4.5),('control boundary','7A8997','FFFFFF',3),('focus','1D4ED8','FFFFFF',3)]
checks=[]
for name,fg,bg,minimum in pairs:
 a,b=sorted([lum(fg),lum(bg)]);ratio=(b+.05)/(a+.05);assert ratio>=minimum,(name,ratio);checks.append({'pair':name,'contrast':round(ratio,2),'minimum':minimum})
summary={'date':'2026-10-09','matrix_rows':len(rows),'source_ids_preserved':True,'implementation_statuses_unchanged':True,'status_counts':dict(collections.Counter(r['status'] for r in rows)),'design_batch_counts':dict(collections.Counter(r['design_batch'] for r in rows)),'contrast_checks':checks,'limitations':['Workstream routing is provisional, not semantic acceptance.','Contrast calculations do not certify rendered UI or WCAG compliance.','No new runtime/backend test executed for these documentation changes.']}
(p/'PHASE-2-VALIDATION.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps(summary,indent=2))
