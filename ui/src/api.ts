let token='';
export async function api<T=any>(path:string,method='GET',body?:unknown):Promise<T>{
 if(method!=='GET'&&!token){const r=await fetch('/api/auth/csrf',{credentials:'same-origin'});if(!r.ok)throw new Error('Conexiunea cu serverul a eșuat. / Server connection failed.');token=(await r.json()).token;}
 const headers:Record<string,string>={};if(method!=='GET'){headers['X-CSRF-TOKEN']=token;headers['Idempotency-Key']=crypto.randomUUID();}if(body&&!(body instanceof FormData))headers['Content-Type']='application/json';
 const response=await fetch('/api'+path,{method,headers,credentials:'same-origin',body:body instanceof FormData?body:body===undefined?undefined:JSON.stringify(body)});
 if(!response.ok){const p=await response.json().catch(()=>({ro:'Server indisponibil.',en:'Server unavailable.'}));const lang=localStorage.getItem('decont.language')||'ro';let message=p[lang]||p.code||response.statusText;try{const issues=JSON.parse(message);if(Array.isArray(issues))message=issues.map(i=>i[lang]).join('\n');}catch{}throw new Error(message);}if(['/auth/login','/auth/logout','/auth/change-password'].includes(path))token='';return response.status===204?undefined as T:response.json();
}
