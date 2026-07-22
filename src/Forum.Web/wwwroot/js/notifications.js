/* =========================================================================
   Thông báo: tải dropdown khi mở chuông, đánh dấu đã đọc, cập nhật badge.
   Forum.notifyNew() được SignalR gọi khi có thông báo mới (rung chuông).
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};
  const $ = (s) => document.querySelector(s);

  function setBadge(count) {
    const b = $("#notif-badge");
    if (!b) return;
    if (count > 0) { b.textContent = count > 99 ? "99+" : count; b.classList.remove("hide"); b.classList.add("badge-pop"); setTimeout(() => b.classList.remove("badge-pop"), 400); }
    else b.classList.add("hide");
  }

  // Tải nội dung dropdown khi mở chuông.
  document.addEventListener("menu:open", async e => {
    const menu = e.detail?.menu;
    if (!menu || menu.id !== "notif-menu") return;
    const body = $("#notif-body");
    if (!body) return;
    body.innerHTML = `<div class="card-pad"><div class="skeleton sk-line w80"></div><div class="skeleton sk-line"></div><div class="skeleton sk-line w60"></div></div>`;
    try { body.innerHTML = await Forum.api.get("/thong-bao/dropdown"); }
    catch { body.innerHTML = `<div class="card-pad muted small">Không tải được.</div>`; }
  });

  // Đánh dấu tất cả đã đọc.
  document.addEventListener("click", async e => {
    if (e.target.closest("#notif-mark-all") || e.target.closest("#notif-mark-all-page")) {
      e.preventDefault();
      try {
        await Forum.api.post("/thong-bao/doc-tat-ca", {});
        document.querySelectorAll(".notif-item.unread").forEach(n => { n.classList.remove("unread"); n.querySelector(".presence-dot")?.remove(); });
        setBadge(0);
        Forum.toast("Đã đánh dấu tất cả đã đọc.", "success", 1500);
      } catch { Forum.toast("Lỗi.", "error"); }
      return;
    }
    // Click vào một thông báo -> đánh dấu đã đọc rồi điều hướng.
    const item = e.target.closest("[data-notif]");
    if (item) {
      const id = item.dataset.notif;
      try { const r = await Forum.api.post(`/thong-bao/${id}/doc`, {}); setBadge(r.count); } catch {}
      // để mặc định điều hướng theo href
    }
  });

  // Hook cho SignalR.
  Forum.notifyNew = function (count) {
    const bell = document.querySelector('[data-menu="notif-menu"]');
    if (bell) { bell.classList.remove("bell-shake"); void bell.offsetWidth; bell.classList.add("bell-shake"); }
    setBadge(count);
  };
})();
