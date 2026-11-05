export function add(a:unknown,b:unknown):string;
export function sub(a:unknown,b:unknown):string;
export function negate(a:unknown):string;
export function compare(a:unknown,b:unknown):number;
export function minimum(a:unknown,b:unknown):string;
export function sum(items:unknown[]):string;
export function formatMoney(value:unknown,locale?:string,decimals?:number):string;

export function masterJson(data:Record<string,unknown>):string;

export function formatOriginal(value:unknown,locale?:string):string;
export function parseFinancialJson(text:string):any;
