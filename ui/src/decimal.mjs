// Decimal-only financial presentation and arithmetic; Number is reserved for geometry/counts.
function parts(value) {
  const text = String(value ?? '0');
  if (!/^-?\d+(?:\.\d+)?$/.test(text))
    throw new Error('Valoare zecimală invalidă / Invalid decimal');
  const negative = text.startsWith('-');
  const [whole, fraction = ''] = text.replace('-', '').split('.');
  return { n: BigInt(whole + fraction) * (negative ? -1n : 1n), s: fraction.length };
}
function render(n, s) {
  const sign = n < 0n ? '-' : '';
  let text = (n < 0n ? -n : n).toString().padStart(s + 1, '0');
  if (s) text = text.slice(0, -s) + '.' + text.slice(-s);
  return sign + text;
}
export function add(a, b) {
  const x = parts(a),
    y = parts(b),
    s = Math.max(x.s, y.s);
  return render(x.n * 10n ** BigInt(s - x.s) + y.n * 10n ** BigInt(s - y.s), s);
}
export function sub(a, b) {
  return add(a, negate(b));
}
export function negate(a) {
  const x = parts(a);
  return render(-x.n, x.s);
}
export function compare(a, b) {
  const x = parts(sub(a, b));
  return x.n < 0n ? -1 : x.n > 0n ? 1 : 0;
}
export function minimum(a, b) {
  return compare(a, b) <= 0 ? String(a) : String(b);
}
export function sum(items) {
  return items.reduce((total, value) => add(total, value), '0');
}
export function formatMoney(value, locale = 'ro-RO', decimals = 2) {
  const p = parts(value);
  let n = p.n;
  const negative = n < 0n;
  if (negative) n = -n;
  if (p.s > decimals) {
    const q = 10n ** BigInt(p.s - decimals);
    n = (n + q / 2n) / q;
  } else n *= 10n ** BigInt(decimals - p.s);
  const text = render(n, decimals);
  const [whole, fraction] = text.split('.');
  const ro = locale.startsWith('ro');
  return (
    (negative && n !== 0n ? '-' : '') +
    whole.replace(/\B(?=(\d{3})+(?!\d))/g, ro ? '.' : ',') +
    (decimals ? (ro ? ',' : '.') + fraction : '')
  );
}

export function masterJson(data) {
  return (
    '{' +
    Object.entries(data)
      .map(
        ([key, value]) =>
          JSON.stringify(key) + ':' + (key === 'ronRate' ? add(value, '0') : JSON.stringify(value)),
      )
      .join(',') +
    '}'
  );
}

export function formatOriginal(value, locale = 'ro-RO') {
  const text = String(value ?? '0');
  return formatMoney(text, locale, Math.max(2, (text.split('.')[1] || '').length));
}
// Legacy snapshots contain JSON numbers. Read monetary tokens before JSON.parse can round them.
export function parseFinancialJson(text) {
  let at = 0;
  const money = new Set([
    'amount',
    'base',
    'vat',
    'estimatedron',
    'settledron',
    'total',
    'personal',
    'financeron',
    'roundingron',
    'principalron',
    'feeron',
    'markupron',
    'ronrate',
    'roundingtoleranceron',
    'debit',
    'credit',
    'gross',
    'paidvat',
    'printedbase',
    'variance',
  ]);
  const ws = () => {
    while (/\s/.test(text[at] || '') && at < text.length) at++;
  };
  function string() {
    const start = at++;
    while (at < text.length) {
      if (text[at] === '\\') {
        at += 2;
        continue;
      }
      if (text[at++] === '"') return JSON.parse(text.slice(start, at));
    }
    throw new SyntaxError('Invalid JSON string');
  }
  function value(key = '') {
    ws();
    const c = text[at];
    if (c === '"') return string();
    if (c === '{') {
      at++;
      const obj = {};
      ws();
      if (text[at] === '}') {
        at++;
        return obj;
      }
      while (true) {
        ws();
        if (text[at] !== '"') throw new SyntaxError('Invalid JSON key');
        const name = string();
        ws();
        if (text[at++] !== ':') throw new SyntaxError('Invalid JSON object');
        Object.defineProperty(obj, name, {
          value: value(
            name === 'total' && (Object.hasOwn(obj, 'versions') || Object.hasOwn(obj, 'page'))
              ? '__count'
              : name,
          ),
          enumerable: true,
          configurable: true,
          writable: true,
        });
        ws();
        const end = text[at++];
        if (end === '}') return obj;
        if (end !== ',') throw new SyntaxError('Invalid JSON object');
      }
    }
    if (c === '[') {
      at++;
      const array = [];
      ws();
      if (text[at] === ']') {
        at++;
        return array;
      }
      while (true) {
        array.push(value());
        ws();
        const end = text[at++];
        if (end === ']') return array;
        if (end !== ',') throw new SyntaxError('Invalid JSON array');
      }
    }
    for (const word of ['true', 'false', 'null'])
      if (text.startsWith(word, at)) {
        at += word.length;
        return word === 'null' ? null : word === 'true';
      }
    const found = /^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/.exec(text.slice(at));
    if (!found) throw new SyntaxError('Invalid JSON value');
    const token = found[0];
    at += token.length;
    if (money.has(key.toLowerCase())) {
      if (/[eE]/.test(token)) throw new SyntaxError('Monetary exponent notation is not supported');
      return token;
    }
    return JSON.parse(token);
  }
  const result = value();
  ws();
  if (at !== text.length) throw new SyntaxError('Unexpected JSON data');
  return result;
}
