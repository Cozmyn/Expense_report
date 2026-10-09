"""Append the read-only companion workbook and explicit conversation clarifications."""
from pathlib import Path
import json,hashlib,collections
from openpyxl import load_workbook
p=Path(__file__).parent
m=json.loads((p/'initial-matrix.json').read_text());l=json.loads((p/'source-ledger.json').read_text());manifest=json.loads((p/'source-manifest.json').read_text())
if any(r['source']=='WORKBOOK' for r in l):raise SystemExit('Already extended')
f=Path('../upload/Expense_Platform_Traceability_and_Requirements_v2_Simple_Auth(1).xlsx')
w=load_workbook(f,read_only=True,data_only=False)
for s in w:
 headers=[]
 for n,row in enumerate(s.iter_rows(values_only=True),1):
  vals=[str(v) if v is not None else '' for v in row]
  if not any(vals):continue
  if not headers:
   headers=vals;l.append(dict(source='WORKBOOK',file=f.name,section=s.title,locator=f'row {n}',text=' | '.join(vals),classification='Table header'));continue
  text='; '.join(f'{headers[i] if i<len(headers) else i+1}: {v}' for i,v in enumerate(vals))
  rid=f'RTM-{len(m)+1:04d}'
  l.append(dict(source='WORKBOOK',file=f.name,section=s.title,locator=f'row {n}',text=text,classification='Obligation',requirement_id=rid))
  equivalent=next((x for x in m if x['source_id']==vals[0] and vals[0].startswith(('BR-','TBR-','FR-','NFR-','TC-','D-','OI-'))),None)
  m.append(dict(id=rid,source_id=vals[0],requirement=text,source_section=f'WORKBOOK / {s.title} / row {n}',status=equivalent['status'] if equivalent else 'Partial',assessment=equivalent['assessment'] if equivalent else 'Pending individual verification; workbook reference retained without assuming implementation.',fix=equivalent['fix'] if equivalent else 'Reconcile with specification and verify exact acceptance behavior.',test=equivalent['test'] if equivalent else 'Verify positive, negative, role and recovery cases.',evidence=[]))
clarifications=[
'Multiple receipts per expense report; each has expense category, receipt date and domestic/foreign supplier selection.',
'Foreign supplier: description, currency and amount; do not force Romanian supplier identification or domestic VAT workflow.',
'Domestic supplier: ask whether the company CUI appears on the receipt; only then request supplier CUI and domestic VAT breakdown.',
'Look up supplier CUI in master data; display known supplier; unknown CUI requires explicit correctness confirmation and later Finance resolution.',
'Without company CUI on receipt: description, currency and amount; avoid requiring domestic VAT accounting details.',
'Capture paid VAT and base at 0%, 11%, 21%; reconstruct gross and reconcile before submission and accounting export.',
'Visible file browse per receipt, plus clearly explained shared-file upload at report completion; prevent submission without full attachment coverage.',
'Internal employee platform without promotional slogans; professional Romanian default and English support.',
'OCR suggestions populate editable receipt fields, require human confirmation, and remain connected to accounting export.',
'Provide actual screenshots of each updated platform page including OCR; label test data and do not substitute conceptual images for runtime evidence.'
]
for n,t in enumerate(clarifications,1):
 rid=f'RTM-{len(m)+1:04d}';l.append(dict(source='CLARIFICATION',file='Conversation',section='Earlier user instructions',locator=f'clarification {n}',text=t,classification='Obligation',requirement_id=rid));m.append(dict(id=rid,source_id=f'CLAR-{n:03d}',requirement=t,source_section=f'Conversation / earlier receipt and screenshot instructions / {n}',status='Partial',assessment='Preserved explicitly; acceptance must be verified against the current implementation.',fix='Verify complete UI-to-API-to-export behavior and repair gaps.',test='Exercise all conditional branches with authorized and unauthorized roles.',evidence=[]))
manifest['sources'].append(dict(source='WORKBOOK',file=f.name,sha256=hashlib.sha256(f.read_bytes()).hexdigest(),sheets=w.sheetnames));manifest['counts']=dict(collections.Counter(x['classification'] for x in l));manifest['requirement_rows']=len(m);manifest['status_counts']=dict(collections.Counter(x['status'] for x in m))
for name,data in [('initial-matrix.json',m),('source-ledger.json',l),('source-manifest.json',manifest)]: (p/name).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
intro='# Matrice inițială / Initial requirements matrix\n\nInitial Phase 1 inventory, not final acceptance. **Partial + pending verification is an unverified status**, not proof of implementation. Repeated source references are intentionally retained; row count is not the number of independent features. Done requires individual evidence. Original historical rules are interpreted through their target dispositions.\n\n'
keys=['id','source_id','requirement','source_section','status','fix','test','assessment']
esc=lambda v:str(v).replace('|',' / ').replace('\n',' ')
(p/'INITIAL-MATRIX.md').write_text(intro+'| ID | Source ID | Requirement | Source section | Status | Fix | Test | Assessment |\n|---|---|---|---|---|---|---|---|\n'+'\n'.join('| '+' | '.join(esc(r[k]) for k in keys)+' |' for r in m)+'\n')
assert len({r['id'] for r in m})==len(m)
assert {r['id'] for r in m}=={r['requirement_id'] for r in l if r['classification']=='Obligation'}
print(json.dumps({'rows':len(m),'status':manifest['status_counts'],'sheets':w.sheetnames},indent=2))
