/* =========================================================================
   Diễn đàn Cửa — core front-end framework (vanilla JS, không phụ thuộc jQuery).
   window.Forum: theme, toast, modal/confirm, dropdown, AJAX, vote, reveal...
   ========================================================================= */
(function () {
  "use strict";

  const Forum = (window.Forum = window.Forum || {});
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const $ = (sel, ctx) => (ctx || document).querySelector(sel);
  const $$ = (sel, ctx) => Array.from((ctx || document).querySelectorAll(sel));
  Forum.$ = $; Forum.$$ = $$;

  /* ---- Antiforgery token ---- */
  Forum.token = () => {
    const el = $('input[name="__RequestVerificationToken"]') || $('meta[name="csrf-token"]');
    return el ? (el.value || el.content) : "";
  };

  /* ---- AJAX helpers ---- */
  Forum.api = {
    async request(url, method, body) {
      const opts = { method, headers: { "X-Requested-With": "XMLHttpRequest" } };
      if (body !== undefined) {
        opts.headers["Content-Type"] = "application/json";
        opts.headers["RequestVerificationToken"] = Forum.token();
        opts.body = JSON.stringify(body);
      }
      const res = await fetch(url, opts);
      const ct = res.headers.get("content-type") || "";
      const data = ct.includes("application/json") ? await res.json() : await res.text();
      if (!res.ok) throw Object.assign(new Error((data && data.message) || "Lỗi"), { status: res.status, data });
      return data;
    },
    get(url) { return this.request(url, "GET"); },
    post(url, body) { return this.request(url, "POST", body || {}); },
    postForm(url, formData) {
      return fetch(url, { method: "POST", headers: { "RequestVerificationToken": Forum.token(), "X-Requested-With": "XMLHttpRequest" }, body: formData })
        .then(async r => { const j = await r.json().catch(() => ({})); if (!r.ok) throw Object.assign(new Error(j.message || "Lỗi"), { data: j }); return j; });
    }
  };

  /* ---- Top progress bar ---- */
  const topbar = (() => {
    let bar, t;
    function ensure() { if (!bar) { bar = document.createElement("div"); bar.id = "topbar"; document.body.appendChild(bar); } return bar; }
    return {
      start() { const b = ensure(); b.classList.add("active"); b.style.width = "0"; requestAnimationFrame(() => b.style.width = "70%"); },
      done() { const b = ensure(); b.style.width = "100%"; clearTimeout(t); t = setTimeout(() => { b.classList.remove("active"); b.style.width = "0"; }, 250); }
    };
  })();
  Forum.progress = topbar;

  /* ---- Toast ---- */
  Forum.toast = function (message, type = "info", timeout = 3200) {
    let stack = $(".toast-stack");
    if (!stack) { stack = document.createElement("div"); stack.className = "toast-stack"; document.body.appendChild(stack); }
    const el = document.createElement("div");
    el.className = "toast " + type;
    el.setAttribute("role", "status");
    el.innerHTML = `<span>${escapeHtml(message)}</span>`;
    stack.appendChild(el);
    const close = () => { el.classList.add("out"); setTimeout(() => el.remove(), 250); };
    el.addEventListener("click", close);
    setTimeout(close, timeout);
    return el;
  };

  /* ---- Modal & confirm ---- */
  Forum.modal = function (innerHtml, opts = {}) {
    const backdrop = document.createElement("div");
    backdrop.className = "backdrop";
    const modal = document.createElement("div");
    modal.className = "modal" + (opts.large ? " lg" : "");
    modal.innerHTML = innerHtml;
    backdrop.appendChild(modal);
    document.body.appendChild(backdrop);
    const close = () => { backdrop.classList.add("closing"); setTimeout(() => backdrop.remove(), 170); };
    backdrop.addEventListener("mousedown", e => { if (e.target === backdrop && opts.dismissable !== false) close(); });
    document.addEventListener("keydown", function esc(e) { if (e.key === "Escape") { close(); document.removeEventListener("keydown", esc); } });
    return { el: modal, backdrop, close };
  };

  Forum.confirm = function (opts = {}) {
    const { title = "Xác nhận", message = "Bạn có chắc chắn?", okText = "Đồng ý", cancelText = "Hủy", danger = true } = opts;
    return new Promise(resolve => {
      const m = Forum.modal(`
        <h3>${escapeHtml(title)}</h3>
        <p class="muted">${escapeHtml(message)}</p>
        <div class="modal-actions">
          <button class="btn" data-act="cancel">${escapeHtml(cancelText)}</button>
          <button class="btn ${danger ? "btn-danger" : "btn-primary"}" data-act="ok">${escapeHtml(okText)}</button>
        </div>`);
      m.el.querySelector('[data-act="ok"]').addEventListener("click", () => { m.close(); resolve(true); });
      m.el.querySelector('[data-act="cancel"]').addEventListener("click", () => { m.close(); resolve(false); });
    });
  };

  /* ---- Theme ---- */
  Forum.theme = {
    get() { return document.documentElement.getAttribute("data-theme") || "light"; },
    set(mode) {
      document.documentElement.setAttribute("data-theme", mode);
      try { localStorage.setItem("theme", mode); } catch (_) {}
      $$("[data-theme-icon]").forEach(i => i.classList.toggle("hide", i.dataset.themeIcon !== mode));
    },
    toggle() { this.set(this.get() === "dark" ? "light" : "dark"); }
  };

  /* ---- Vote control ---- */
  async function handleVote(btn) {
    const wrap = btn.closest("[data-vote]");
    if (!wrap) return;
    const target = wrap.dataset.vote;              // "topic" | "comment"
    const id = wrap.dataset.id;
    const value = parseInt(btn.dataset.value, 10); // 1 | -1
    if (!Forum.isAuthenticated) { Forum.toast("Vui lòng đăng nhập để bình chọn.", "warning"); return; }

    btn.classList.remove("bounce"); void btn.offsetWidth; btn.classList.add("bounce");
    try {
      const r = await Forum.api.post(`/vote/${target}`, { id: parseInt(id, 10), value });
      const scoreEl = wrap.querySelector(".vote-score");
      countUp(scoreEl, r.score);
      scoreEl.classList.toggle("pos", r.score > 0);
      scoreEl.classList.toggle("neg", r.score < 0);
      const up = wrap.querySelector(".vote-btn.up"), down = wrap.querySelector(".vote-btn.down");
      up && up.classList.toggle("on", r.userVote === 1);
      down && down.classList.toggle("on", r.userVote === -1);
      if (r.userVote === 1 && up) { up.classList.remove("vote-glow"); void up.offsetWidth; up.classList.add("vote-glow"); }
    } catch (e) {
      Forum.toast(e.message || "Không thể bình chọn.", "error");
    }
  }

  function countUp(el, to) {
    if (!el) return;
    const from = parseInt(el.textContent.replace(/[^\d-]/g, ""), 10) || 0;
    if (reduceMotion || from === to) { el.textContent = to; return; }
    const steps = Math.min(12, Math.abs(to - from)); let i = 0;
    const step = () => { i++; el.textContent = Math.round(from + (to - from) * (i / steps)); if (i < steps) requestAnimationFrame(step); else el.textContent = to; };
    requestAnimationFrame(step);
  }
  Forum.countUp = countUp;

  /* ---- Ripple ---- */
  function ripple(e, btn) {
    const r = document.createElement("span");
    const d = Math.max(btn.clientWidth, btn.clientHeight);
    const rect = btn.getBoundingClientRect();
    r.className = "ripple";
    r.style.width = r.style.height = d + "px";
    r.style.left = (e.clientX - rect.left - d / 2) + "px";
    r.style.top = (e.clientY - rect.top - d / 2) + "px";
    btn.appendChild(r);
    setTimeout(() => r.remove(), 520);
  }

  /* ---- Dropdown ---- */
  function closeMenus(except) {
    $$(".menu.open").forEach(m => { if (m !== except) { m.classList.remove("open"); m.hidden = true; } });
  }
  Forum.closeMenus = closeMenus;
  document.addEventListener("click", e => {
    const trigger = e.target.closest("[data-menu]");
    if (trigger) {
      e.preventDefault();
      const menu = document.getElementById(trigger.dataset.menu);
      if (menu) {
        const willOpen = menu.hidden;
        closeMenus(menu);
        menu.hidden = !willOpen;
        menu.classList.toggle("open", willOpen);
        if (willOpen) trigger.dispatchEvent(new CustomEvent("menu:open", { bubbles: true, detail: { menu } }));
      }
      return;
    }
    if (!e.target.closest(".menu")) closeMenus();

    const vbtn = e.target.closest(".vote-btn");
    if (vbtn) { e.preventDefault(); handleVote(vbtn); return; }

    const btn = e.target.closest(".btn");
    if (btn && !reduceMotion) ripple(e, btn);

    const themeToggle = e.target.closest("[data-theme-toggle]");
    if (themeToggle) { e.preventDefault(); Forum.theme.toggle(); }

    const social = e.target.closest("[data-social]");
    if (social) { e.preventDefault(); Forum.toast(`Đăng nhập bằng ${social.dataset.social} sẽ sớm có mặt (cần cấu hình OAuth).`, "info"); }

    const ham = e.target.closest("[data-toggle-sidebar]");
    if (ham) { e.preventDefault(); $$(".left-rail, .sidebar").forEach(el => el.classList.toggle("show")); }
    const searchToggle = e.target.closest("[data-toggle-search]");
    if (searchToggle) { e.preventDefault(); const hs = $(".header-search.collapsible"); if (hs) hs.classList.toggle("show"); }
  });

  /* ---- Image lightbox (xem ảnh khi click: chat + nội dung bài) ---- */
  document.addEventListener("click", e => {
    const img = e.target.closest(".chat-img, .md img");
    if (!img || !img.getAttribute("src")) return;
    e.preventDefault();
    const box = document.createElement("div");
    box.className = "lightbox";
    const full = document.createElement("img");
    full.src = img.src; full.alt = img.alt || "";
    box.appendChild(full);
    function close() { box.remove(); document.removeEventListener("keydown", onKey); }
    function onKey(ev) { if (ev.key === "Escape") close(); }
    box.addEventListener("click", close);
    document.addEventListener("keydown", onKey);
    document.body.appendChild(box);
  });

  /* ---- Confirm-on-submit forms ([data-confirm]) ---- */
  document.addEventListener("submit", async e => {
    const form = e.target;
    if (form.dataset.confirm !== undefined && !form.dataset.confirmed) {
      e.preventDefault();
      const ok = await Forum.confirm({ message: form.dataset.confirm || "Bạn có chắc chắn?" });
      if (ok) { form.dataset.confirmed = "1"; form.submit(); }
    }
  });

  /* ---- Reveal-on-scroll ---- */
  Forum.observeReveal = function (root) {
    const els = $$(".reveal:not(.shown)", root);
    if (!els.length) return;
    if (reduceMotion || !("IntersectionObserver" in window)) { els.forEach(el => el.classList.add("shown")); return; }
    const io = new IntersectionObserver((entries, obs) => {
      entries.forEach(en => { if (en.isIntersecting) { en.target.classList.add("shown"); obs.unobserve(en.target); } });
    }, { rootMargin: "0px 0px -40px 0px" });
    els.forEach(el => io.observe(el));
  };

  /* ---- nav progress on internal link click ---- */
  document.addEventListener("click", e => {
    const a = e.target.closest("a");
    if (a && a.href && a.origin === location.origin && !a.target && !a.hasAttribute("data-no-progress") && (a.getAttribute("href") || "#")[0] !== "#") {
      topbar.start();
    }
  });
  window.addEventListener("pageshow", () => topbar.done());

  function escapeHtml(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c])); }
  Forum.escapeHtml = escapeHtml;

  /* ---- Lưu/bỏ lưu chủ đề (toàn cục: hoạt động cả ở feed lẫn trang chi tiết) ---- */
  document.addEventListener("click", async e => {
    const bm = e.target.closest("[data-bookmark]");
    if (!bm) return;
    e.preventDefault();
    if (!Forum.isAuthenticated) { Forum.toast("Vui lòng đăng nhập để lưu chủ đề.", "warning"); return; }
    // Hiệu ứng ngôi sao nhảy lên ngay khi bấm.
    const star = bm.querySelector("svg");
    if (star) { star.classList.remove("star-jump"); void star.offsetWidth; star.classList.add("star-jump"); }
    try {
      const r = await Forum.api.post(`/chu-de/${bm.dataset.bookmark}/luu`, {});
      bm.classList.toggle("is-saved", r.bookmarked);
      const lbl = bm.querySelector(".bm-label");
      if (lbl) lbl.textContent = r.bookmarked ? "Đã lưu" : "Lưu";
      Forum.toast(r.bookmarked ? "Đã lưu chủ đề." : "Đã bỏ lưu.", "success", 1500);
    } catch (err) { Forum.toast(err.message || "Không thực hiện được.", "error"); }
  });

  /* ---- init ---- */
  document.addEventListener("DOMContentLoaded", () => {
    Forum.theme.set(Forum.theme.get());
    Forum.observeReveal();
    // Đã BỎ hiệu ứng .page-enter cho .main-col: trước đây class được thêm sau khi trang
    // đã vẽ → nội dung hiện ra rồi bị ẩn đi và trượt lại (giật). Nội dung giờ hiển thị
    // ngay, không animate cả cột; riêng thẻ chủ đề vẫn fade nhẹ qua .reveal.
  });
})();
