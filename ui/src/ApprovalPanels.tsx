import React,{useEffect,useState}from'react';
import{api}from'./api';
import{Field}from'./Fields';
type Any=Record<string,any>;

export function ApprovalHistory({detail,t}:Any){
 const routes=detail.approvalRoutes||[],tasks=detail.approvalTasks||[];
 if(!routes.length)return null;
 const labels:Any={Pending:t('În așteptare','Pending'),Approved:t('Aprobat','Approved'),Returned:t('Returnat','Returned'),Rejected:t('Respins definitiv','Rejected permanently'),Reassigned:t('Reasignat','Reassigned')};
 return <details className="panel approval-history"><summary>{t('Traseul aprobării','Approval route')} · {t('Revizia','Revision')} {detail.report.revision}</summary><ol className="approval-timeline">{routes.map((route:Any)=>{const data=JSON.parse(route.snapshotJson),task=tasks.find((x:Any)=>x.routeId===route.id);return <li key={route.id}><strong>{t('Revizia','Revision')} {route.revision} · {data.managerName}</strong><p>{data.assigneeId!==data.managerId?t('Delegat: ','Delegate: '):t('Responsabil: ','Assignee: ')}{data.assigneeName} · {labels[task?.state]||t('Istoric','History')}</p><small>{new Date(route.createdAt).toLocaleString()} · {data.department||'—'}</small>{task?.note&&<p>{task.note}</p>}</li>;})}</ol></details>;
}

export function AdminReassignment({detail,t,run,refresh}:Any){
 const r=detail.report,[users,U]=useState<Any[]>([]),[manager,M]=useState(''),[reason,R]=useState('');
 useEffect(()=>{run(async()=>U(await api('/directory')));},[]);
 if(!['MANAGER_REVIEW','REAPPROVAL_REQUIRED'].includes(r.state))return null;
 const pending=(detail.approvalTasks||[]).find((x:Any)=>x.state==='Pending');
 return <section className="panel"><h2>{t('Reasignare aprobare','Reassign approval')}</h2><p className="muted">{t('Actualizează mai întâi alocarea organizațională. Revizia trimisă rămâne aceeași; fostul responsabil pierde accesul la aprobarea în curs.','Update the organisation assignment first. The submitted revision stays unchanged; the former assignee loses access to the pending approval.')}</p><div className="form-grid"><Field name={t('Noul manager','New manager')}><select value={manager} onChange={e=>M(e.target.value)}><option value="">{t('Selectează','Select')}</option>{users.filter(x=>x.roles.includes('Manager')&&x.id!==r.employeeId).map(x=><option key={x.id} value={x.id}>{x.name} · {x.worker}</option>)}</select></Field><Field name={t('Motiv reasignare','Reassignment reason')}><input value={reason} minLength={3} onChange={e=>R(e.target.value)}/></Field></div><button className="secondary" disabled={!manager||reason.trim().length<3} onClick={()=>run(async()=>{if(!window.confirm(t('Confirmi reasignarea acestei aprobări?','Reassign this approval?')))return;await api('/admin/reports/'+r.id+'/reassign','POST',{version:pending?.version||0,reportVersion:r.version,managerId:manager,reason});await refresh();R('');})}>{t('Reasignează aprobarea','Reassign approval')}</button></section>;
}

export function OrganisationReview({t,run,open}:Any){
 const[data,D]=useState<Any|null>(null),[users,U]=useState<Any[]>([]),[assignments,A]=useState<Any[]>([]),[query,Q]=useState('');
 const load=async()=>{const[d,u,a]=await Promise.all([api('/admin/approval-issues'),api('/admin/users'),api('/admin/assignments')]);D(d);U(u);A(a);};
 useEffect(()=>{run(load);},[]);
 const today=new Date().toISOString().slice(0,10),active=assignments.filter(x=>x.active&&x.from<=today&&(!x.to||x.to>=today));
 const name=(id:string)=>users.find(x=>x.id===id)?.name||'—';
 return <section className="panel"><div className="section-heading"><h1>{t('Organigramă și aprobări','Organisation and approvals')}</h1><button className="secondary" onClick={()=>run(load)}>{t('Actualizează','Refresh')}</button></div><p className="muted">{t('O singură companie. Managerul este stabilit de Admin; schimbările nu mută automat aprobările deja trimise.','One company. Admin assigns managers; changes do not automatically move submitted approvals.')}</p>{!data?<p role="status">{t('Se verifică alocările…','Checking assignments…')}</p>:<><h2>{t('Necesită intervenție','Needs attention')} ({data.people.length+data.reports.length})</h2>{!data.people.length&&!data.reports.length&&<p className="success">{t('Nu există probleme de rutare.','No routing issues found.')}</p>}{data.people.map((x:Any)=><div className="issue warning" key={x.id}><strong>{x.name} · {x.worker}</strong><p>{t(x.ro,x.en)}</p></div>)}{data.reports.map((x:Any)=><div className="simple-row" key={x.id}><span>{x.number} · {name(x.employeeId)}<small>{t('Aprobarea veche necesită confirmarea Admin. Deschide decontul pentru reasignare.','Legacy approval needs Admin confirmation. Open the report to reassign it.')}</small></span><button className="secondary" onClick={()=>open(x.id)}>{t('Deschide','Open')}</button></div>)}</>}<h2>{t('Alocări curente','Current assignments')}</h2><Field name={t('Caută angajat sau LantID','Search employee or LantID')}><input type="search" value={query} onChange={e=>Q(e.target.value)}/></Field><div className="table-wrap"><table><thead><tr><th>{t('Angajat','Employee')}</th><th>LantID</th><th>{t('Departament','Department')}</th><th>Manager</th><th>{t('Valabilitate','Effective dates')}</th></tr></thead><tbody>{users.filter(x=>x.roles.includes('Employee')&&(x.name+' '+x.worker).toLowerCase().includes(query.toLowerCase())).map(x=>{const a=active.find(a=>a.userId===x.id);return <tr key={x.id}><td>{x.name}</td><td>{x.worker}</td><td>{a?.organizationCode||'—'}</td><td>{a?name(a.managerId):t('Nealocat','Unassigned')}</td><td>{a?a.from+' → '+(a.to||'∞'):'—'}</td></tr>;})}</tbody></table></div></section>;
}
