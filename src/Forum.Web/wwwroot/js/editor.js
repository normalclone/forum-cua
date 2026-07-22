/* =========================================================================
   Markdown editor: thanh công cụ + tab "Xem trước" (render server-side để khớp).
   Nâng cấp mọi <textarea data-md-editor>.
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};

  const TOOLS = [
    { t: "B", title: "Đậm", wrap: ["**", "**"], style: "font-weight:800" },
    { t: "I", title: "Nghiêng", wrap: ["*", "*"], style: "font-style:italic" },
    { t: "H", title: "Tiêu đề", line: "## " },
    { t: "“ ”", title: "Trích dẫn", line: "> " },
    { t: "• List", title: "Danh sách", line: "- " },
    { t: "1.", title: "Danh sách số", line: "1. " },
    { t: "🔗", title: "Liên kết", wrap: ["[", "](https://)"] },
    { t: "🖼", title: "Ảnh", wrap: ["![mô tả ảnh](", ")"] },
    { t: "</>", title: "Code", wrap: ["`", "`"] },
    { t: "{ }", title: "Khối code", wrap: ["\n```\n", "\n```\n"] },
  ];

  function enhance(ta) {
    if (ta.dataset.enhanced) return;
    ta.dataset.enhanced = "1";

    const wrap = document.createElement("div");
    wrap.className = "editor";
    ta.parentNode.insertBefore(wrap, ta);

    const tabs = document.createElement("div");
    tabs.className = "editor-tabs";
    tabs.innerHTML = `<button type="button" class="active" data-tab="write">Viết</button><button type="button" data-tab="preview">Xem trước</button>`;

    const toolbar = document.createElement("div");
    toolbar.className = "editor-toolbar";
    TOOLS.forEach((tool) => {
      const b = document.createElement("button");
      b.type = "button"; b.title = tool.title; b.textContent = tool.t;
      if (tool.style) b.setAttribute("style", tool.style);
      b.addEventListener("click", () => { applyTool(ta, tool); ta.focus(); });
      toolbar.appendChild(b);
    });

    // Nút tải ảnh/tệp lên.
    const upBtn = document.createElement("button");
    upBtn.type = "button"; upBtn.title = "Tải ảnh/tệp lên"; upBtn.textContent = "📎";
    const fileInput = document.createElement("input");
    fileInput.type = "file"; fileInput.style.display = "none";
    fileInput.accept = "image/*,.pdf,.doc,.docx,.xls,.xlsx,.zip,.txt";
    upBtn.addEventListener("click", () => fileInput.click());
    fileInput.addEventListener("change", async () => {
      const f = fileInput.files[0]; if (!f) return;
      const fd = new FormData(); fd.append("file", f);
      upBtn.textContent = "⏳";
      try {
        const r = await Forum.api.postForm("/tai-len", fd);
        const md = r.isImage ? `![${f.name}](${r.url})` : `[${r.name}](${r.url})`;
        const pos = ta.selectionStart;
        ta.value = ta.value.slice(0, pos) + md + ta.value.slice(pos);
        ta.dispatchEvent(new Event("input", { bubbles: true }));
        Forum.toast("Đã tải lên.", "success", 1500);
      } catch (err) { Forum.toast(err.message || "Tải lên thất bại.", "error"); }
      finally { upBtn.textContent = "📎"; fileInput.value = ""; }
    });
    toolbar.appendChild(upBtn);
    toolbar.appendChild(fileInput);

    const preview = document.createElement("div");
    preview.className = "editor-preview md hide";

    wrap.appendChild(tabs);
    wrap.appendChild(toolbar);
    wrap.appendChild(ta);
    wrap.appendChild(preview);

    tabs.addEventListener("click", async e => {
      const btn = e.target.closest("[data-tab]");
      if (!btn) return;
      tabs.querySelectorAll("button").forEach(x => x.classList.remove("active"));
      btn.classList.add("active");
      const isPreview = btn.dataset.tab === "preview";
      ta.classList.toggle("hide", isPreview);
      toolbar.classList.toggle("hide", isPreview);
      preview.classList.toggle("hide", !isPreview);
      if (isPreview) {
        preview.innerHTML = `<span class="muted small">Đang render…</span>`;
        try {
          const r = await Forum.api.post("/markdown/preview", { markdown: ta.value });
          preview.innerHTML = r.html || `<span class="muted">Không có nội dung.</span>`;
        } catch { preview.innerHTML = `<span class="muted">Không xem trước được.</span>`; }
      }
    });
  }

  function applyTool(ta, tool) {
    const start = ta.selectionStart, end = ta.selectionEnd;
    const val = ta.value, sel = val.slice(start, end);
    if (tool.line) {
      const lineStart = val.lastIndexOf("\n", start - 1) + 1;
      ta.value = val.slice(0, lineStart) + tool.line + val.slice(lineStart);
      ta.selectionStart = ta.selectionEnd = end + tool.line.length;
    } else if (tool.wrap) {
      const [a, b] = tool.wrap;
      ta.value = val.slice(0, start) + a + sel + b + val.slice(end);
      ta.selectionStart = start + a.length;
      ta.selectionEnd = start + a.length + sel.length;
    }
    ta.dispatchEvent(new Event("input", { bubbles: true }));
  }

  Forum.enhanceEditors = (root) => (root || document).querySelectorAll("textarea[data-md-editor]:not([data-enhanced])").forEach(enhance);

  document.addEventListener("DOMContentLoaded", () => Forum.enhanceEditors());
})();
