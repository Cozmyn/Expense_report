import{readFile}from'node:fs/promises';
import{pathToFileURL}from'node:url';
export const gates=['automatedTests','financeGoldenCases','hrOrganisation','erpTestImport','privacySecurity','accessibilityUat','measuredPerformance','coordinatedRestore','monitoringOnCall','trainingCutover'];
export function missingEvidence(value){
 const missing=[];const text=x=>typeof x==='string'&&x.trim().length>=3&&!/^(pending|todo|example|null)$/i.test(x.trim());
 const dated=x=>typeof x==='string'&&Number.isFinite(Date.parse(x))&&Date.parse(x)<=Date.now();
 if(value.schemaVersion!==1)missing.push('schemaVersion');if(!/^[a-f0-9]{40}$/.test(value.releaseSha||''))missing.push('releaseSha');if(value.companyScope!=='single-company')missing.push('companyScope');
 for(const name of gates){const gate=value.gates?.[name];if(gate?.status!=='accepted'||!text(gate.owner)||!text(gate.evidence)||!dated(gate.approvedAt))missing.push(name);}
 for(const name of['rpoMinutes','rtoMinutes'])if(!(typeof value.targets?.[name]==='number'&&value.targets[name]>0))missing.push(name);
 const decision=value.releaseDecision;if(decision?.status!=='approved'||!text(decision.owner)||!text(decision.signedEvidence)||!dated(decision.approvedAt))missing.push('signedReleaseDecision');
 return missing;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 try{const value=JSON.parse(await readFile(process.argv[2]||'deploy/release-evidence.example.json','utf8'));const missing=missingEvidence(value);console.log(JSON.stringify({releaseSha:value.releaseSha,readyForHumanEvidenceReview:missing.length===0,missing,notice:'References must be independently verified. This check does not authenticate signatories or authorize deployment.'},null,2));if(missing.length)process.exitCode=1;}catch{console.error('Cannot read release evidence. Supply a valid manifest path.');process.exitCode=2;}
}
