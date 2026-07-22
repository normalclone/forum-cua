/* =========================================================================
   Soạn chủ đề: thẻ dạng chip, tự lưu nháp, bật/tắt poll, cảnh báo khi rời trang.
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};
  const $ = (s, c) => (c || document).querySelector(s);

  /* ---- Tag chips ---- */
  const tagWrap = $("[data-tag-input]");
  const tagValue = $("[data-tag-value]");
  if (tagWrap && tagValue) {
    const input = tagWrap.querySelector("[data-tag-add]");
    let tags = (tagValue.value || "").split(",").map(s => s.trim()).filter(Boolean);

    function sync() { tagValue.value = tags.join(", "); tagValue.dispatchEvent(new Event("input", { bubbles: true })); }
    function render() {
      tagWrap.querySelectorAll(".chip").forEach(c => c.remove());
      tags.forEach(t => {
        const chip = document.createElement("span");
        chip.className = "chip";
        chip.innerHTML = `#${Forum.escapeHtml(t)} <button type="button" aria-label="Xóa">✕</button>`;
        chip.querySelector("button").addEventListener("click", () => remove(t, chip));
        tagWrap.insertBefore(chip, input);
      });
    }
    function add(text) {
      text = text.trim().replace(/^#/, "");
      if (!text || tags.length >= 6 || tags.some(x => x.toLowerCase() === text.toLowerCase())) return;
      tags.push(text); sync();
      const chip = document.createElement("span");
      chip.className = "chip";
      chip.innerHTML = `#${Forum.escapeHtml(text)} <button type="button" aria-label="Xóa">✕</button>`;
      chip.querySelector("button").addEventListener("click", () => remove(text, chip));
      tagWrap.insertBefore(chip, input);
    }
    function remove(text, chip) {
      tags = tags.filter(x => x !== text); sync();
      chip.classList.add("removing"); setTimeout(() => chip.remove(), 150);
    }
    input.addEventListener("keydown", e => {
      if (e.key === "Enter" || e.key === ",") { e.preventDefault(); add(input.value); input.value = ""; }
      else if (e.key === "Backspace" && !input.value && tags.length) {
        const last = tags[tags.length - 1]; const chip = [...tagWrap.querySelectorAll(".chip")].pop();
        if (chip) remove(last, chip);
      }
    });
    input.addEventListener("blur", () => { if (input.value.trim()) { add(input.value); input.value = ""; } });
    render();
  }

  /* ---- Poll toggle ---- */
  const pollToggle = $("[data-poll-toggle]");
  if (pollToggle) {
    const fields = $("#poll-fields");
    pollToggle.addEventListener("change", () => fields.classList.toggle("hide", !pollToggle.checked));
  }

  /* ---- Draft autosave + unsaved warning (chỉ trang tạo mới) ---- */
  const form = $("[data-compose]");
  const status = $("[data-draft-status]");
  if (form) {
    let dirty = false, submitting = false, timer;

    form.addEventListener("input", () => {
      dirty = true;
      if (status) { clearTimeout(timer); timer = setTimeout(saveDraft, 1500); }
    });
    form.addEventListener("submit", () => { submitting = true; dirty = false; });

    async function saveDraft() {
      if (!status) return;
      const cat = form.querySelector("[name=CategoryId]");
      try {
        const r = await Forum.api.post("/nhap/luu", {
          categoryId: cat && cat.value ? parseInt(cat.value, 10) : null,
          title: form.querySelector("[name=Title]")?.value,
          body: form.querySelector("[name=Body]")?.value,
          tags: form.querySelector("[name=Tags]")?.value
        });
        const d = new Date();
        status.textContent = `Đã lưu nháp lúc ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
      } catch { /* im lặng */ }
    }

    // Khôi phục nháp nếu form trống.
    if (status) {
      Forum.api.get("/nhap").then(d => {
        if (!d) return;
        const titleEl = form.querySelector("[name=Title]"), bodyEl = form.querySelector("[name=Body]");
        if ((titleEl && titleEl.value) || (bodyEl && bodyEl.value)) return;
        if (d.title) titleEl.value = d.title;
        if (d.body) { bodyEl.value = d.body; bodyEl.dispatchEvent(new Event("input", { bubbles: true })); }
        if (d.categoryId) { const c = form.querySelector("[name=CategoryId]"); if (c) c.value = d.categoryId; }
        if (d.tags && tagValue) tagValue.value = d.tags;
        status.textContent = "Đã khôi phục bản nháp";
      }).catch(() => {});
    }

    window.addEventListener("beforeunload", e => {
      if (dirty && !submitting) { e.preventDefault(); e.returnValue = ""; }
    });
  }
})();
