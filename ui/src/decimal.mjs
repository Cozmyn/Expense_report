// Decimal-only financial presentation and arithmetic; Number is reserved for geometry/counts.
function parts(value){const text=String(value??'0');if(!/^-?\d+(?:\.\d+)?$/.test(text))throw new Error('Valoare zecimală invalidă / Invalid decimal');const negative=text.startsWith('-');const [whole,fraction='']=text.replace('-','').split('.');return{n:BigInt(whole+fraction)*(negative?-1n:1n),s:fraction.length};}
function render(n,s){const sign=n<0n?'-':'';let text=(n<0n?-n:n).toString().padStart(s+1,'0');if(s)text=text.slice(0,-s)+'.'+text.slice(-s);return sign+text;}
export function add(a,b){const x=parts(a),y=parts(b),s=Math.max(x.s,y.s);return render(x.n*10n**BigInt(s-x.s)+y.n*10n**BigInt(s-y.s),s);}
export function sub(a,b){return add(a,negate(b));}
export function negate(a){const x=parts(a);return render(-x.n,x.s);}
export function compare(a,b){const x=parts(sub(a,b));return x.n<0n?-1:x.n>0n?1:0;}
export function minimum(a,b){return compare(a,b)<=0?String(a):String(b);}
export function sum(items){return items.reduce((total,value)=>add(total,value),'0');}
export function formatMoney(value,locale='ro-RO',decimals=2){const p=parts(value);let n=p.n;const negative=n<0n;if(negative)n=-n;if(p.s>decimals){const q=10n**BigInt(p.s-decimals);n=(n+q/2n)/q;}else n*=10n**BigInt(decimals-p.s);const text=render(n,decimals);const [whole,fraction]=text.split('.');const ro=locale.startsWith('ro');return(negative&&n!==0n?'-':'')+whole.replace(/\B(?=(\d{3})+(?!\d))/g,ro?'.':',')+(decimals?(ro?',':'.')+fraction:'');}

export function masterJson(data){return '{'+Object.entries(data).map(([key,value])=>JSON.stringify(key)+':'+(key==='ronRate'?add(value,'0'):JSON.stringify(value))).join(',')+'}';}
