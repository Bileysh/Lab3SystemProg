"use strict";

const $ = (id) => document.getElementById(id);
const source = $("source"), gutter = $("gutter"), lineMark = $("lineMark");
const LINE_H = 18, PAD = 6;
let lastResult = null, errorLines = new Set();

function updateGutter() {
  const n = source.value.split("\n").length;
  let html = "";
  for (let i = 1; i <= n; i++) html += errorLines.has(i) ? `<span class="err">${i}</span>\n` : i + "\n";
  gutter.innerHTML = html;
  gutter.scrollTop = source.scrollTop;
  positionMark();
}

let markedLine = 0;
function markLine(line, scroll = true) {
  markedLine = line;
  if (!line) { lineMark.style.display = "none"; return; }
  if (scroll) {
    const top = (line - 1) * LINE_H;
    if (top < source.scrollTop || top > source.scrollTop + source.clientHeight - 3 * LINE_H)
      source.scrollTop = Math.max(0, top - source.clientHeight / 3);
  }
  positionMark();
}
function positionMark() {
  if (!markedLine) return;
  lineMark.style.display = "block";
  lineMark.style.top = PAD + (markedLine - 1) * LINE_H - source.scrollTop + "px";
}

source.addEventListener("input", () => { errorLines.clear(); updateGutter(); });
source.addEventListener("scroll", () => { gutter.scrollTop = source.scrollTop; positionMark(); });
source.addEventListener("keydown", (e) => {
  if (e.key === "Tab") {
    e.preventDefault();
    const s = source.selectionStart;
    source.setRangeText("    ", s, source.selectionEnd, "end");
    updateGutter();
  } else if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
    e.preventDefault();
    compile();
  }
});

document.querySelectorAll(".tabs button").forEach((b) =>
  b.addEventListener("click", () => showTab(b.dataset.tab)));
function showTab(name) {
  document.querySelectorAll(".tabs button").forEach((b) => b.classList.toggle("active", b.dataset.tab === name));
  document.querySelectorAll(".tab-body").forEach((t) => t.classList.toggle("active", t.id === "tab-" + name));
  if (name === "ast" && lastResult?.ast) fitTree(0);
}

async function loadExamples() {
  const list = await (await fetch("/api/examples")).json();
  const sel = $("examples");
  sel.innerHTML = `<option value="">- приклади -</option>` + list.map((f) => `<option>${f}</option>`).join("");
  sel.addEventListener("change", async () => {
    if (!sel.value) return;
    source.value = await (await fetch("/api/examples/" + encodeURIComponent(sel.value))).text();
    errorLines.clear();
    updateGutter();
    compile();
  });
  sel.value = list.includes("factorial.c") ? "factorial.c" : list[0];
  sel.dispatchEvent(new Event("change"));
}

$("compileBtn").addEventListener("click", compile);

async function compile() {
  const btn = $("compileBtn");
  btn.disabled = true;
  try {
    const resp = await fetch("/api/compile", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ source: source.value, name: $("examples").value || "input.c", trace: $("traceToggle").checked }),
    });
    const res = await resp.json();
    lastResult = res;
    render(res);
  } catch (e) {
    $("diagnostics").innerHTML = `<div class="diag error">помилка запиту: ${esc(e.message)}</div>`;
  } finally {
    btn.disabled = false;
  }
}

function render(res) {
  const g = res.grammar;
  $("grammarStats").textContent = `граматика: ${g.rules} правил, ${g.states} станів LALR(1); розбір ${res.parseMs.toFixed(1)} мс`;

  errorLines = new Set(res.errors.map((e) => e.line).filter(Boolean));
  const diags = [...res.errors.map((e) => ({ ...e, cls: "error" })), ...res.warnings.map((w) => ({ ...w, cls: "warning" }))];
  $("diagnostics").innerHTML = diags.map((d) =>
    `<div class="diag ${d.cls}" data-line="${d.line}"><span class="stage">${d.line ? "рядок " + d.line : ""}</span>` +
    `${esc(d.stage)}: ${esc(d.message)}</div>`).join("");
  $("diagnostics").querySelectorAll(".diag").forEach((el) =>
    el.addEventListener("click", () => markLine(+el.dataset.line)));
  updateGutter();
  markLine(res.errors[0]?.line || 0);

  renderTokens(res.tokens);
  renderTrace(res.trace);
  $("jsOut").innerHTML = res.javaScript ? highlightJs(res.javaScript) : `<span class="com">// код не згенеровано - є помилки</span>`;

  if (res.ast) drawTree(res.ast);
  else {
    $("tree").innerHTML = `<div class="placeholder">AST не побудовано через синтаксичну помилку.</div>`;
  }

  const it = res.interpreter;
  $("interpOut").textContent = it ? it.stdout + (it.error ? `\n[помилка виконання, рядок ${it.error.line}] ${it.error.message}` : "") : "-";
  $("interpMeta").textContent = it ? `код повернення ${it.exitCode ?? "-"}, кроків ${it.steps}, ${it.ms.toFixed(1)} мс` : "";
  $("jsRunOut").textContent = "";
  $("jsRunMeta").textContent = "";
  $("matchBadge").textContent = "";
  $("matchBadge").className = "";
  if (res.javaScript) runInBrowser(res.javaScript).then((r) => {
    $("jsRunOut").textContent = r.text + (r.error ? "\n" + r.error : "");
    $("jsRunMeta").textContent = r.error ? "" : `код повернення ${r.code}, ${r.ms.toFixed(1)} мс`;
    const same = it && !r.error && r.text === it.stdout;
    $("matchBadge").textContent = same ? "вивід інтерпретатора і JS збігається" : "вивід інтерпретатора і JS відрізняється";
    $("matchBadge").className = same ? "ok" : "bad";
  });
}

function runInBrowser(js) {
  return new Promise((resolve) => {
    const src = "self.__cOutput = (text, code) => postMessage({ text, code });\n" + js;
    const url = URL.createObjectURL(new Blob([src], { type: "text/javascript" }));
    const w = new Worker(url);
    const t0 = performance.now();
    const done = (r) => { clearTimeout(timer); w.terminate(); URL.revokeObjectURL(url); resolve({ ms: performance.now() - t0, ...r }); };
    const timer = setTimeout(() => done({ text: "", error: "Перевищено час виконання (5 с)" }), 5000);
    w.onmessage = (e) => done(e.data);
    w.onerror = (e) => { e.preventDefault(); done({ text: "", error: "Помилка JS: " + e.message }); };
  });
}

function renderTokens(tokens) {
  $("tokensTable").querySelector("tbody").innerHTML = tokens.map((t, i) =>
    `<tr data-line="${t.line}"><td>${i + 1}</td><td>${t.line}:${t.column}</td><td class="tok">${esc(t.type)}</td><td>${esc(t.text)}</td></tr>`).join("");
  $("tokensTable").querySelectorAll("tr[data-line]").forEach((tr) =>
    tr.addEventListener("click", () => markLine(+tr.dataset.line)));
}

function renderTrace(lines) {
  if (!lines.length) {
    $("traceOut").innerHTML = `<span class="stack">Траса вимкнена: позначте «траса» і перекомпілюйте.</span>`;
    return;
  }
  let step = 0;
  $("traceOut").innerHTML = lines.map((l) => {
    const e = esc(l);
    if (l.startsWith("Shifting")) return `<span class="shift">${String(++step).padStart(5)}  SHIFT   ${e.replace("Shifting token ", "").replace(", Entering state", " → стан")}</span>`;
    if (l.startsWith("Reducing")) return `<span class="reduce">${String(++step).padStart(5)}  REDUCE  ${e.replace("Reducing stack by rule", "за правилом")}</span>`;
    if (l.startsWith("State stack")) return `<span class="stack">        стек станів: ${e.replace("State stack is now: ", "")}</span>`;
    if (l.startsWith("Entering state")) return null;
    const t = e.replace("Reading: Next token is", "вхідна лексема:").replace("Next token is still", "вхідна лексема (та сама):")
      .replace(", Entering state", ", перехід у стан");
    return `<span class="stack">        ${t}</span>`;
  }).filter((x) => x !== null).join("\n");
}

function highlightJs(code) {
  const re = /(\/\/[^\n]*|\/\*[\s\S]*?\*\/)|("(?:\\.|[^"\\])*")|\b(function|let|const|return|if|else|for|while|do|break|continue|try|catch|throw|new|typeof|of)\b|\b(\d+(?:\.\d+)?)\b/g;
  let out = "", last = 0;
  for (const m of code.matchAll(re)) {
    out += esc(code.slice(last, m.index));
    const cls = m[1] ? "com" : m[2] ? "str" : m[3] ? "kw" : "num";
    out += `<span class="${cls}">${esc(m[0])}</span>`;
    last = m.index + m[0].length;
  }
  return out + esc(code.slice(last));
}

$("copyJs").addEventListener("click", () => lastResult?.javaScript && navigator.clipboard.writeText(lastResult.javaScript));
$("saveJs").addEventListener("click", () => {
  if (!lastResult?.javaScript) return;
  download(new Blob([lastResult.javaScript], { type: "text/javascript" }),
    ($("examples").value || "program.c").replace(/\.c$/, "") + ".js");
});

const CATEGORIES = [
  ["program", "програма"], ["func", "функція"], ["decl", "оголошення"], ["type", "тип"],
  ["stmt", "оператор"], ["expr", "вираз"], ["ident", "ідентифікатор"], ["literal", "літерал"],
];
$("legend").innerHTML = CATEGORIES.map(([c, t]) => `<span style="--c: var(--c-${c})">■ ${t}</span>`).join("");

const color = (cat) => getComputedStyle(document.documentElement).getPropertyValue("--c-" + cat).trim() || "#888";
const cssVar = (v) => getComputedStyle(document.documentElement).getPropertyValue(v).trim();

let svg, gRoot, zoom, root, selectedNode = null, uid = 0;
const NODE_H = 22, LEVEL_H = 64, CHAR_W = 7.1;

function drawTree(data) {
  const box = $("tree");
  box.innerHTML = "";
  svg = d3.select(box).append("svg");
  gRoot = svg.append("g");
  gRoot.append("g").attr("class", "links");
  gRoot.append("g").attr("class", "nodes");
  zoom = d3.zoom().scaleExtent([0.05, 3]).on("zoom", (e) => gRoot.attr("transform", e.transform));
  svg.call(zoom).on("dblclick.zoom", null);

  root = d3.hierarchy(data, (d) => d.children);
  root.each((d) => {
    d.id = ++uid;
    d.w = Math.max(30, d.data.name.length * CHAR_W + 14);
  });
  collapseBelow(root, 6);
  update(root, false);
  fitTree(0);
}

function collapseBelow(node, depth) {
  node.each((d) => {
    if (d.children && d.depth >= depth) { d._children = d.children; d.children = null; }
  });
}

function expandAll(node) {
  node.each((d) => { if (d._children) { d.children = d._children; d._children = null; } });
}

function update(source, animate = true) {
  const layout = d3.tree().nodeSize([1, LEVEL_H]).separation((a, b) => (a.w + b.w) / 2 + (a.parent === b.parent ? 14 : 28));
  layout(root);
  const tr = (sel) => (animate ? sel.transition().duration(350) : sel);
  const nodes = root.descendants(), links = root.links();

  const node = gRoot.select(".nodes").selectAll("g.node").data(nodes, (d) => d.id);
  const enter = node.enter().append("g").attr("class", "node")
    .attr("transform", `translate(${source.x0 ?? source.x},${source.y0 ?? source.y})`)
    .attr("opacity", 0)
    .on("click", (e, d) => onNodeClick(d))
    .on("mouseenter", (e, d) => showTip(e, d))
    .on("mousemove", moveTip)
    .on("mouseleave", hideTip);
  enter.append("rect")
    .attr("x", (d) => -d.w / 2).attr("y", -NODE_H / 2)
    .attr("width", (d) => d.w).attr("height", NODE_H).attr("rx", 2)
    .attr("fill", cssVar("--code-bg"))
    .attr("stroke", (d) => color(d.data.category));
  enter.append("text").attr("text-anchor", "middle").attr("dy", "0.35em")
    .attr("fill", (d) => color(d.data.category)).text((d) => d.data.name);
  enter.append("text").attr("class", "role").attr("text-anchor", "middle").attr("y", -NODE_H / 2 - 5)
    .attr("fill", cssVar("--muted")).text((d) => d.data.role || "");

  const merged = enter.merge(node);
  merged.classed("collapsed", (d) => !!d._children).classed("selected", (d) => d === selectedNode);
  merged.select("rect").attr("fill", (d) => d._children ? cssVar("--bar") : cssVar("--code-bg"));
  tr(merged).attr("transform", (d) => `translate(${d.x},${d.y})`).attr("opacity", 1);
  tr(node.exit()).attr("transform", `translate(${source.x},${source.y})`).attr("opacity", 0).remove();

  const path = (s, t) => `M${s.x},${s.y + NODE_H / 2}C${s.x},${(s.y + t.y) / 2} ${t.x},${(s.y + t.y) / 2} ${t.x},${t.y - NODE_H / 2 - 14}`;
  const link = gRoot.select(".links").selectAll("path.link").data(links, (d) => d.target.id);
  const linkAll = link.enter().append("path").attr("class", "link").attr("stroke", cssVar("--edge"))
    .attr("d", () => { const o = { x: source.x0 ?? source.x, y: source.y0 ?? source.y }; return path(o, o); })
    .merge(link);
  tr(linkAll).attr("d", (d) => path(d.source, d.target));
  tr(link.exit()).attr("d", () => path(source, source)).remove();

  nodes.forEach((d) => { d.x0 = d.x; d.y0 = d.y; });
}

function onNodeClick(d) {
  selectedNode = d;
  if (d.data.line) markLine(d.data.line);
  if (d.children) { d._children = d.children; d.children = null; }
  else if (d._children) { d.children = d._children; d._children = null; }
  update(d);
}

function fitTree(dur = 500) {
  if (!svg) return;
  const bounds = gRoot.node().getBBox();
  const { width, height } = svg.node().getBoundingClientRect();
  if (!bounds.width || !width) return;
  const k = Math.min(1.2, 0.92 * Math.min(width / bounds.width, height / bounds.height));
  const tx = width / 2 - k * (bounds.x + bounds.width / 2);
  const ty = Math.max(20, height / 2 - k * (bounds.y + bounds.height / 2));
  svg.transition().duration(dur).call(zoom.transform, d3.zoomIdentity.translate(tx, ty).scale(k));
}

$("expandAll").addEventListener("click", () => { if (root) { expandAll(root); update(root); setTimeout(fitTree, 380); } });
$("collapseDeep").addEventListener("click", () => {
  if (!root) return;
  expandAll(root);
  collapseBelow(root, 3);
  update(root);
  setTimeout(fitTree, 380);
});
$("fitTree").addEventListener("click", () => fitTree());
$("saveSvg").addEventListener("click", () => {
  if (!svg) return;
  const clone = svg.node().cloneNode(true);
  clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
  const b = gRoot.node().getBBox();
  clone.setAttribute("viewBox", `${b.x - 20} ${b.y - 30} ${b.width + 40} ${b.height + 60}`);
  clone.querySelector("g").removeAttribute("transform");
  clone.querySelectorAll("text").forEach((t) => t.setAttribute("font-family", "Consolas, monospace"));
  clone.querySelectorAll("text:not(.role)").forEach((t) => t.setAttribute("font-size", "12"));
  clone.querySelectorAll("text.role").forEach((t) => t.setAttribute("font-size", "10"));
  clone.querySelectorAll("path").forEach((p) => p.setAttribute("fill", "none"));
  download(new Blob([clone.outerHTML], { type: "image/svg+xml" }), "ast.svg");
});

const tip = $("tooltip");
function showTip(e, d) {
  const info = Object.entries(d.data.info || {}).map(([k, v]) => `<div><span class="k">${esc(k)}:</span> ${esc(v)}</div>`).join("");
  tip.innerHTML = `<div>${esc(d.data.name)}</div><div><span class="k">вузол:</span> ${esc(d.data.kind)}</div>` +
    (d.data.role ? `<div><span class="k">роль:</span> ${esc(d.data.role)}</div>` : "") +
    (d.data.line ? `<div><span class="k">рядок:</span> ${d.data.line}</div>` : "") + info +
    (d._children ? `<div class="k">згорнуто: ${d._children.length} дочірн.</div>` : "");
  tip.hidden = false;
  moveTip(e);
  if (d.data.line) markLine(d.data.line, false);
}
function moveTip(e) {
  const r = $("tab-ast").getBoundingClientRect();
  tip.style.left = Math.min(e.clientX - r.left + 14, r.width - 330) + "px";
  tip.style.top = e.clientY - r.top + 14 + "px";
}
function hideTip() { tip.hidden = true; }

function esc(s) {
  return String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);
}
function download(blob, name) {
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

window.addEventListener("resize", () => positionMark());
updateGutter();
loadExamples();
