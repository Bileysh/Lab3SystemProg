"use strict";
const _out = [];

function _idiv(a, b) {
  if (b === 0) throw new Error("Ділення на нуль");
  return Math.trunc(a / b);
}

function _imod(a, b) {
  if (b === 0) throw new Error("Ділення на нуль");
  return a % b;
}

function _arr(dims, fill, init) {
  if (typeof init === "string") init = Array.from(init, (c) => c.charCodeAt(0));
  const [n, ...rest] = dims;
  const a = new Array(n);
  for (let i = 0; i < n; i++) {
    const v = init !== undefined && i < init.length ? init[i] : undefined;
    a[i] = rest.length ? _arr(rest, fill, v) : v !== undefined ? v : fill;
  }
  return a;
}

function _str(v) {
  if (!Array.isArray(v)) return String(v);
  let s = "";
  for (const c of v) {
    if (c === 0) break;
    s += String.fromCharCode(c);
  }
  return s;
}

function _exp(d, prec) {
  return d.toExponential(prec).replace(/e([+-])(\d)$/, "e$10$2");
}

function _float(d, prec, conv, alt) {
  const upper = conv === conv.toUpperCase();
  if (Number.isNaN(d)) return upper ? "NAN" : "nan";
  if (!Number.isFinite(d)) return upper ? "INF" : "inf";
  let r;
  switch (conv.toLowerCase()) {
    case "f":
      r = d.toFixed(prec);
      return alt && prec === 0 ? r + "." : r;
    case "e":
      r = _exp(d, prec);
      return upper ? r.toUpperCase() : r;
    default: {
      const p = prec === 0 ? 1 : prec;
      const es = _exp(d, p - 1);
      const x = parseInt(es.slice(es.indexOf("e") + 1), 10);
      r = p > x && x >= -4 ? d.toFixed(p - 1 - x) : es;
      if (!alt) {
        let [m, e] = r.split("e");
        if (m.includes(".")) m = m.replace(/0+$/, "").replace(/\.$/, "");
        r = e === undefined ? m : m + "e" + e;
      }
      return upper ? r.toUpperCase() : r;
    }
  }
}

function _format(fmt, args) {
  let ai = 0;
  const next = () => (ai < args.length ? args[ai++] : 0);
  const re = /%([-+ #0]*)(\d+|\*)?(?:\.(\d*|\*))?(?:hh|h|ll|l|L|z|j|t)?([diouxXfFeEgGcs%])/g;
  return fmt.replace(re, (_m, flags, w, p, conv) => {
    if (conv === "%") return "%";
    const width = w === undefined ? 0 : w === "*" ? Math.trunc(next()) : parseInt(w, 10);
    const prec = p === undefined ? null : p === "*" ? Math.trunc(next()) : p === "" ? 0 : parseInt(p, 10);
    const v = next();
    const left = flags.includes("-");
    let zero = flags.includes("0") && !left;
    const plus = flags.includes("+"), space = flags.includes(" "), alt = flags.includes("#");
    let sign = "", body;
    if ("diu".includes(conv)) {
      let n = Math.trunc(Number(v));
      if (n < 0) { sign = "-"; n = -n; } else if (plus) sign = "+"; else if (space) sign = " ";
      body = String(n);
      if (prec !== null) { body = body.padStart(prec, "0"); zero = false; }
    } else if ("oxX".includes(conv)) {
      const n = Math.trunc(Number(v));
      body = n.toString(conv === "o" ? 8 : 16);
      if (conv === "X") body = body.toUpperCase();
      if (prec !== null) { body = body.padStart(prec, "0"); zero = false; }
      if (alt && n !== 0) body = (conv === "o" ? "0" : conv === "x" ? "0x" : "0X") + body;
    } else if ("fFeEgG".includes(conv)) {
      let d = Number(v);
      if (d < 0 || Object.is(d, -0)) { sign = "-"; d = -d; } else if (plus) sign = "+"; else if (space) sign = " ";
      body = _float(d, prec === null ? 6 : prec, conv, alt);
      if (!Number.isFinite(d)) zero = false;
    } else if (conv === "c") {
      body = typeof v === "string" ? v[0] : String.fromCharCode(v);
      zero = false;
    } else {
      body = _str(v);
      if (prec !== null && prec < body.length) body = body.slice(0, prec);
      zero = false;
    }
    const pad = width - sign.length - body.length;
    if (pad <= 0) return sign + body;
    if (left) return sign + body + " ".repeat(pad);
    if (zero) return sign + "0".repeat(pad) + body;
    return " ".repeat(pad) + sign + body;
  });
}

function _printf(fmt, ...args) {
  const s = _format(_str(fmt), args);
  _out.push(s);
  return s.length;
}

function _puts(s) {
  _out.push(_str(s) + "\n");
  return 0;
}

function _putchar(c) {
  _out.push(String.fromCharCode(c));
  return c;
}

function _finish(code) {
  const text = _out.join("");
  if (typeof globalThis.__cOutput === "function") globalThis.__cOutput(text, code);
  else if (typeof process !== "undefined" && process.stdout) {
    process.stdout.write(text);
    process.exitCode = code;
  } else console.log(text);
}
