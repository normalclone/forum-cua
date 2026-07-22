/* =========================================================================
   Gợi ý tìm kiếm tức thì (autocomplete) cho ô tìm kiếm trên header.
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};
  const form = document.querySelector("[data-autocomplete]");
  if (!form) return;
  const input = form.querySelector("input[name=q]");
  const menu = form.querySelector("#ac-menu");
  let timer, lastQ = "";

  function close() { menu.hidden = true; menu.innerHTML = ""; }

  async function run() {
    const q = input.value.trim();
    if (q.length < 2) { close(); return; }
    if (q === lastQ) return; lastQ = q;
    try {
      const r = await Forum.api.get(`/tim-kiem/goi-y?q=${encodeURIComponent(q)}`);
      const parts = [];
      if (r.topics?.length) {
        parts.push(`<div class="xsmall muted" style="padding:6px 10px;">Chủ đề</div>`);
        r.topics.forEach(t => parts.push(
          `<a href="${t.url}"><span style="flex:1">${Forum.escapeHtml(t.title)}</span><span class="xsmall muted">${Forum.escapeHtml(t.category || "")}</span></a>`));
      }
      if (r.tags?.length) {
        parts.push(`<div class="xsmall muted" style="padding:6px 10px;">Thẻ</div>`);
        r.tags.forEach(t => parts.push(
          `<a href="/the/${t.slug}">#${Forum.escapeHtml(t.name)} <span class="xsmall muted">${t.useCount}</span></a>`));
      }
      if (!parts.length) parts.push(`<div class="muted small" style="padding:10px;">Không có gợi ý. Nhấn Enter để tìm.</div>`);
      menu.innerHTML = parts.join("");
      menu.hidden = false;
      menu.classList.add("open");
    } catch { close(); }
  }

  input.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(run, 220); });
  input.addEventListener("focus", () => { if (input.value.trim().length >= 2) run(); });
  document.addEventListener("click", e => { if (!form.contains(e.target)) close(); });
  input.addEventListener("keydown", e => { if (e.key === "Escape") close(); });
})();
