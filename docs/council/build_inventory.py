"""Extract ordered DOCX requirements without silently discarding tables or narrative.
Run from repository root: python docs/council/build_inventory.py --source-dir ../upload
Outputs the initial, deliberately conservative audit baseline; never overwrites a reviewed matrix.
"""
from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET
import json,hashlib,re,argparse,html,collections
p=argparse.ArgumentParser();p.add_argument('--source-dir',type=Path,required=True);a=p.parse_args()
out=Path(__file__).parent;ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
sources=[('SPEC','Expense_Management_Platform_Final_Specification_v2_Simple_Auth_RO(1).docx'),('UX','Expense_Platform_Product_Process_UX_Brief_RO(1).docx')]
ledger=[];matrix=[];manifest=[]
def text(el):return ''.join(el.itertext()) if False else ''.join(el.findall('.//w:t',ns)[i].text or '' for i in range(len(el.findall('.//w:t',ns))))
def add(code,name,section,locator,value,kind,sourceid='',acceptance=''):
 rec={'source':code,'file':name,'section':section,'locator':locator,'text':value,'classification':kind}
 ledger.append(rec)
 if kind!='Obligation':return
 rid=f'RTM-{len(matrix)+1:04d}';rec['requirement_id']=rid
 matrix.append({'id':rid,'source_id':sourceid,'requirement':value,'source_section':f'{code} / {section} / {locator}','status':'Partial','assessment':'Not yet individually verified; conservative initial status, not a claim of partial implementation.','fix':'Complete field/action/permission mapping and resolve any uncovered behavior.','test':acceptance or 'Derive positive, negative, permission and recovery acceptance cases from this source item.','evidence':[]})
for code,name in sources:
 f=a.source_dir/name;data=f.read_bytes();root=ET.fromstring(ZipFile(f).read('word/document.xml'));section='Document metadata';started=False;pi=ti=0
 manifest.append({'source':code,'file':name,'sha256':hashlib.sha256(data).hexdigest()})
 for el in root.find('w:body',ns):
  if el.tag.endswith('}p'):
   pi+=1;s=text(el).strip()
   if not s:continue
   style=el.find('w:pPr/w:pStyle',ns);st=style.attrib.get('{'+ns['w']+'}val','') if style is not None else ''
   heading=st.lower().startswith('heading') or bool(re.match(r'^(\d+(?:\.\d+)*\.?(?:\s)|Appendix [A-Z]\.)',s))
   if heading:
    section=s
    if s.startswith('1. '):started=True
    add(code,name,section,f'paragraph {pi}',s,'Heading');continue
   context=not started or section.startswith(('2. Current','3. Excel','4. Current','11. Risks')) or s.startswith(('Layout note:','Exemplu','Reference URLs:','http'))
   add(code,name,section,f'paragraph {pi}',s,'Context' if context else 'Obligation')
  elif el.tag.endswith('}tbl'):
   ti+=1;headers=[]
   for ri,row in enumerate(el.findall('w:tr',ns),1):
    cells=[text(c).strip() for c in row.findall('w:tc',ns)];s=' | '.join(cells)
    if not any(cells):continue
    if ri==1:headers=cells;add(code,name,section,f'table {ti} row {ri}',s,'Table header');continue
    val='; '.join(f'{headers[i] if i<len(headers) else i+1}: {v}' for i,v in enumerate(cells))
    sid=cells[0] if re.match(r'^(BR-|TBR-|FR-|NFR-|TC-|D-|OI-|R-|PUB-|EMP-|MGR-|FIN-|REP-|ADM-|AUD-|SHR-)',cells[0]) else ''
    add(code,name,section,f'table {ti} row {ri}',val,'Obligation' if started else 'Context',sid,cells[-1] if any('acceptance' in h.lower() for h in headers) else '')
    # Explicit leaf rows for the proposed database fields and each role permission.
    if section.startswith('16.1') and len(cells)>=2:
     for field in cells[1].split(','):
      add(code,name,section,f'table {ti} row {ri} field {field.strip()}',f'{cells[0]}.{field.strip()}: preserve semantics, constraints, access and audit; equivalent schema requires documented disposition.','Obligation')
    if 'Permission matrix' in section or section.startswith('Appendix B. Access'):
     for idx,cell in enumerate(cells[1:],1):add(code,name,section,f'table {ti} row {ri} role {headers[idx]}',f'{cells[0]} / {headers[idx]}: {cell}','Obligation',acceptance='Exercise this permission with isolated role and combined roles; verify API and UI.')
# Latest user additions are individually tracked; they cannot replace original rows.
new=[
('Guided forms with steps, progress, autosave, defaults and inline validation','Partial'),('CUI, IBAN, phone and date input formatting where applicable','Partial'),('Mobile touch targets and complete keyboard navigation','Partial'),('Status explanations and next steps per role','Partial'),('Back-office search, saved views, filters, sorting, column control and bulk actions','Partial'),('Confirmation and undo for destructive operations','Partial'),('Queues, SLAs and exception-first dashboards','Partial'),('Drag-and-drop and camera intake for PDF JPG PNG and multipage documents','Partial'),('Real extraction with document-type detection','Partial'),('Calibrated or clearly characterized numeric field confidence and low-confidence highlighting','Missing'),('Original on left and extracted fields on right','Missing'),('Click a field to highlight its coordinates on the original','Missing'),('Store corrections as explicit extraction feedback for future improvement','Partial'),('CUI checksum, applicable IBAN checks, VAT/date/amount consistency and duplicate checks','Partial'),('Document quality/format errors with recovery tips','Partial'),('Preview zoom, rotation and page navigation','Missing'),('Read-only pre-submit/approval summary matching PDF/print/export semantics','Partial'),('Immutable who/what/when/from-where/before-after audit','Partial'),('Auditor export, revision history and permitted restore actions','Partial'),('Master-data duplicate prevention, merge tools and reference propagation','Partial'),('Mandatory master fields, validation, CSV/XLSX import/export and error reports','Partial'),('Master ownership, active status and change history','Partial'),('Full RO/EN catalogs, visible remembered language switch and no hardcoded UI strings','Partial'),('Bilingual glossary, Romanian diacritics and consistent concise action labels','Missing'),('Locale-aware dates numbers and currency','Partial'),('Reusable design tokens components loading empty error and success states','Partial'),('WCAG 2.2 AA keyboard focus labels contrast and assistive technology checks','Partial'),('Pagination or virtualization and measured load performance','Partial'),('Short onboarding, contextual help and isolated sample-data training','Missing'),('Typed API contracts and clean separation of concerns','Partial'),('OWASP-oriented auth RBAC validation rate limiting and secret management','Partial'),('Transactional integrity database constraints and migrations','Partial'),('Critical-flow unit integration E2E and regression coverage','Partial'),('Reproducible Docker CI and documented environment configuration','Partial'),('RO/EN architecture user guide API docs README changelog and owner QA checklist','Partial'),('All original requirements tracked with justified dispositions and phase updates','Partial'),('No dead actions broken flows production placeholders or mock data','Partial')]
for n,(s,status) in enumerate(new,1):
 add('REQUEST','User premium audit request 2026-10-09','Functional/UX/technical quality',f'addition {n}',s,'Obligation',f'PREM-{n:03d}')
 matrix[-1]['status']=status;matrix[-1]['assessment']='Initial source review; verify with implementation and dedicated acceptance evidence.'
# Concrete findings from inspected source; applied to every repeated source reference.
findings={
'BR-011':('Broken','Posting.cs uses raw Taxes.Count; Rules.VatCodeCount is only used by reporting.','Apply tolerance-aware posting branch selection while preserving base/gross money.','Golden cases: 0% base 0, 1.99, 2.00, 2.01; mixed 11/21 and vehicle, with FX.'),
'TBR-013':('Broken','Posting.cs uses raw Taxes.Count; Rules.VatCodeCount is only used by reporting.','Apply tolerance-aware posting branch selection while preserving base/gross money.','Same small-zero boundary and ERP row assertions.'),
'FR-VAT-004':('Broken','Small-zero count not connected to posting; frontend arithmetic tolerance uses currency units.','Unify RON validation and posting tolerance semantics.','Foreign-currency VAT boundary and client/server parity tests.'),
'TBR-027':('Partial','Finance.cs sets RetainUntil at posting; Reports.cs close does not extend it; no lifecycle worker.','Specify final posting/closure anchor and implement legal-hold-aware lifecycle.','Clock-controlled post/close/reopen/hold tests; storage lifecycle acceptance.'),
'FR-EMP-004':('Partial','DocumentExtraction.cs has real asynchronous multipage OCR; Confidence defaults null, no coordinates.','Add provider confidence/geometry, document classification and reviewed correction feedback.','Real varied RO/EN receipts, multipage invoices, quality failures and calibration tests.'),
'FR-EMP-006':('Partial','FieldStates exists, but labels such as project optional remain hardcoded under required policy.','Apply all field states consistently and focus exact invalid field.','Required/optional/N/A matrix across all policy dimensions.'),
'FR-FIN-001':('Partial','Saved Finance filters exist; no server pagination/column control; general list duplicates queue.','Build one paged queue and context-preserving workbench.','Role-scoped filters, sorting, deep links, 10k-report performance.'),
'FR-ERP-001':('Partial','Posting engine exists but tolerance gap and company golden fixtures remain.','Repair deterministic branches and validate approved import examples.','Company-approved 24-column expected rows and totals.'),
'FR-AUD-002':('Partial','Audit search/export exists; 1k/50k caps and raw codes/JSON reduce completeness/usability.','Paged history and complete bounded streaming export with readable diffs.','Export beyond 50k events; filter parity; unauthorized access denial.'),
'SHR-02':('Missing','main.tsx has no My Profile route; normal password change is not exposed after first login.','Add minimal profile and self-service password change view.','Change password, revoke old sessions, preserve identity and forbid role editing.')}
# Import existing source references as leads only, not proof of completion.
req=out.parent/'REQUIREMENTS.md';leads={}
if req.exists():
 for l in req.read_text().splitlines():
  c=[x.strip() for x in l.strip('|').split('|')]
  if c and c[0].startswith('FR-') and len(c)>3:leads[c[0]]=c[3]
for row in matrix:
 sid=row['source_id']
 if sid in leads:row['evidence']=['Prior matrix lead (not independently accepted): '+leads[sid]]
 if sid in findings:
  row['status'],row['assessment'],row['fix'],row['test']=findings[sid]
# No Done until an individual acceptance review has been signed off for this audit.
manifest_data={'baseline_commit':'d23bc5639abbb1269d5f17c368673b945a457df6','sources':manifest,'counts':dict(collections.Counter(x['classification'] for x in ledger)),'requirement_rows':len(matrix),'status_counts':dict(collections.Counter(x['status'] for x in matrix)),'coverage_definition':'Every extracted nonempty body paragraph/table row classified. This is source-inventory accounting, NOT proof of semantic completeness or implementation acceptance. Compound requirements still need leaf-case review.','source_inventory_complete':all(x['classification']!='Obligation' or 'requirement_id' in x for x in ledger)}
for filename,value in [('initial-matrix.json',matrix),('source-ledger.json',ledger),('source-manifest.json',manifest_data)]:
 target=out/filename
 if target.exists():raise SystemExit('Refusing to overwrite reviewed baseline: '+str(target))
 target.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
intro='# Matrice inițială de trasabilitate / Initial traceability matrix\n\nPhase 1 baseline. Status Partial with assessment “Not yet individually verified” means pending verification, not confirmed partial implementation. No Done claims are made by automated extraction. Source IDs are preserved alongside stable numbered RTM IDs. Historical logic is interpreted through its target disposition.\n\n'
lines=[intro,'| ID | Source ID | Requirement | Source section | Status | Fix | Test | Assessment |','|---|---|---|---|---|---|---|---|']
def esc(x):return str(x).replace('|',' / ').replace('\n',' ')
for r in matrix:lines.append('| '+' | '.join(esc(r[k]) for k in ['id','source_id','requirement','source_section','status','fix','test','assessment'])+' |')
(out/'INITIAL-MATRIX.md').write_text('\n'.join(lines)+'\n')
print(json.dumps(manifest_data,ensure_ascii=False,indent=2))
