/* =========================================================================
   Hover card — preview khi hover/focus vào:
     • tên người dùng  (.user-link[data-username]      → /thanh-vien/{u}/the)
     • chủ đề/bài       ([data-topic-preview]           → /chu-de/{id}/the)
   Delay 350ms, cache AJAX, định vị quanh CON TRỎ (ưu tiên phía trên, rồi
   dưới/phải/trái tuỳ chỗ trống), fade+scale.
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const hoverNone = window.matchMedia("(hover: none)").matches;
  const cache = new Map();
  const SELECTOR = ".user-link[data-username], [data-topic-preview]";

  // Mỗi loại anchor → khoá cache + endpoint riêng.
  function infoOf(anchor) {
    if (anchor.dataset.username)
      return { key: "u:" + anchor.dataset.username, url: `/thanh-vien/${encodeURIComponent(anchor.dataset.username)}/the` };
    if (anchor.dataset.topicPreview)
      return { key: "t:" + anchor.dataset.topicPreview, url: `/chu-de/${encodeURIComponent(anchor.dataset.topicPreview)}/the` };
    return null;
  }

  const GAP = 14;                 // khoảng hở giữa con trỏ và ô preview
  const clamp = (v, lo, hi) => Math.max(lo, Math.min(v, hi));
  let card, arrow, showTimer, hideTimer, currentAnchor;
  let pointer = { x: 0, y: 0 };   // vị trí con trỏ (toạ độ viewport)

  function ensureCard() {
    if (card) return card;
    card = document.createElement("div");
    card.className = "hovercard";
    card.hidden = true;
    arrow = document.createElement("div");
    arrow.className = "arrow";
    document.body.appendChild(card);
    card.addEventListener("mouseenter", () => clearTimeout(hideTimer));
    card.addEventListener("mouseleave", scheduleHide);
    return card;
  }

  async function loadCard(info) {
    if (cache.has(info.key)) return cache.get(info.key);
    const skeleton = `<div class="hc-top"><div class="skeleton" style="width:40px;height:40px;border-radius:50%"></div>
        <div style="flex:1"><div class="skeleton sk-line w60"></div><div class="skeleton sk-line w40"></div></div></div>
        <div class="skeleton sk-line w80"></div><div class="skeleton sk-line"></div>`;
    ensureCard().innerHTML = skeleton;
    try {
      const html = await Forum.api.get(info.url);
      cache.set(info.key, html);
      return html;
    } catch {
      return `<div class="muted small">Không tải được nội dung.</div>`;
    }
  }

  // Định vị ô preview quanh con trỏ. Ưu tiên: TRÊN → dưới → phải → trái,
  // chọn phía đầu tiên còn đủ chỗ; nếu không phía nào đủ thì vẫn ưu tiên trên (đã kẹp biên).
  function place() {
    const cw = card.offsetWidth, ch = card.offsetHeight;
    const vw = window.innerWidth, vh = window.innerHeight;
    const sx = window.scrollX, sy = window.scrollY;
    const m = 8;
    const px = pointer.x, py = pointer.y;

    const fit = {
      top: py - GAP >= ch + m,
      bottom: vh - py - GAP >= ch + m,
      right: vw - px - GAP >= cw + m,
      left: px - GAP >= cw + m
    };
    const p = fit.top ? "top" : fit.bottom ? "bottom" : fit.right ? "right" : fit.left ? "left" : "top";

    let top, left, oX = "left", oY = "top";
    if (p === "top" || p === "bottom") {
      left = clamp(px - cw / 2, m, vw - cw - m);
      top = p === "top" ? py - GAP - ch : py + GAP;
      top = clamp(top, m, vh - ch - m);
      oY = p === "top" ? "bottom" : "top";
      oX = px - left < cw / 2 ? "left" : "right";
    } else {
      top = clamp(py - ch / 2, m, vh - ch - m);
      left = p === "right" ? px + GAP : px - GAP - cw;
      left = clamp(left, m, vw - cw - m);
      oX = p === "right" ? "left" : "right";
      oY = py - top < ch / 2 ? "top" : "bottom";
    }

    card.style.top = (top + sy) + "px";
    card.style.left = (left + sx) + "px";
    card.style.setProperty("--hc-origin", `${oY} ${oX}`);
    card.dataset.placement = p;   // phía đã chọn (top/bottom/right/left) — phục vụ kiểm thử
    positionArrow(p, px, py, left, top, cw, ch);
  }

  // Mũi tên trỏ về phía con trỏ trên cạnh tương ứng.
  function positionArrow(p, px, py, left, top, cw, ch) {
    if (!arrow) return;
    if (p === "top" || p === "bottom") {
      arrow.style.top = (p === "top" ? ch - 5 : -5) + "px";
      arrow.style.left = clamp(px - left - 5, 12, cw - 22) + "px";
      arrow.style.transform = `rotate(${p === "top" ? 225 : 45}deg)`;
    } else {
      arrow.style.left = (p === "right" ? -5 : cw - 5) + "px";
      arrow.style.top = clamp(py - top - 5, 12, ch - 22) + "px";
      arrow.style.transform = `rotate(${p === "right" ? 315 : 135}deg)`;
    }
    if (!card.contains(arrow)) card.appendChild(arrow);
  }

  async function show(anchor) {
    const info = infoOf(anchor);
    if (!info) return;
    currentAnchor = anchor;
    const html = await loadCard(info);
    if (currentAnchor !== anchor) return; // di chuột đi nơi khác trong lúc tải
    ensureCard();
    card.innerHTML = html;
    card.classList.toggle("hovercard-topic", info.key[0] === "t");  // preview chủ đề rộng hơn
    card.hidden = false;
    card.classList.remove("out");
    place();
    if (!reduceMotion) { card.classList.remove("in"); void card.offsetWidth; card.classList.add("in"); }
  }

  function scheduleShow(anchor) {
    clearTimeout(hideTimer);
    clearTimeout(showTimer);
    showTimer = setTimeout(() => show(anchor), 350);
  }
  function scheduleHide() {
    clearTimeout(showTimer);
    hideTimer = setTimeout(hide, 220);
  }
  function hide() {
    if (!card || card.hidden) return;
    currentAnchor = null;
    if (reduceMotion) { card.hidden = true; return; }
    card.classList.add("out");
    setTimeout(() => { if (card) card.hidden = true; }, 130);
  }

  // Trên thiết bị cảm ứng, chạm vào liên kết sẽ điều hướng tới hồ sơ (mặc định <a>).
  // Vẫn gắn hover/focus cho desktop (kể cả headless) để không phụ thuộc media (hover).
  // Luôn theo dõi vị trí con trỏ để ô preview xuất hiện ngay quanh chuột.
  document.addEventListener("mousemove", e => { pointer.x = e.clientX; pointer.y = e.clientY; }, { passive: true });
  document.addEventListener("mouseover", e => {
    const a = e.target.closest(SELECTOR);
    if (a) { pointer.x = e.clientX; pointer.y = e.clientY; scheduleShow(a); }
  });
  document.addEventListener("mouseout", e => {
    const a = e.target.closest(SELECTOR);
    if (a) scheduleHide();
  });
  // keyboard accessibility — không có con trỏ → đặt mốc tại tâm phần tử.
  document.addEventListener("focusin", e => {
    const a = e.target.closest(SELECTOR);
    if (a) {
      const r = a.getBoundingClientRect();
      pointer = { x: r.left + r.width / 2, y: r.top + r.height / 2 };
      show(a);
    }
  });
  document.addEventListener("focusout", e => {
    if (e.target.closest(SELECTOR)) scheduleHide();
  });
  document.addEventListener("keydown", e => { if (e.key === "Escape") hide(); });

  /* ---- Follow toggle (card + trang hồ sơ) ---- */
  document.addEventListener("click", async e => {
    const btn = e.target.closest("[data-follow]");
    if (!btn) return;
    e.preventDefault();
    if (!Forum.isAuthenticated) { Forum.toast("Vui lòng đăng nhập để theo dõi.", "warning"); return; }
    const username = btn.dataset.follow;
    try {
      const r = await Forum.api.post(`/thanh-vien/${encodeURIComponent(username)}/theo-doi`, {});
      cache.delete(username);
      document.querySelectorAll(`[data-follow="${CSS.escape(username)}"]`).forEach(b => {
        b.textContent = r.following ? "Đang theo dõi" : "Theo dõi";
        b.classList.toggle("btn-primary", !r.following);
      });
      const fc = document.getElementById("follower-count");
      if (fc) Forum.countUp(fc, r.followers);
      Forum.toast(r.following ? "Đã theo dõi." : "Đã bỏ theo dõi.", "success", 1800);
    } catch (err) {
      Forum.toast(err.message || "Không thực hiện được.", "error");
    }
  });
})();
