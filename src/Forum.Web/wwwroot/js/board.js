/* =========================================================================
   Board real-time (/hubs/forum): đẩy thông báo (rung chuông) + chủ đề mới.
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};
  if (typeof signalR === "undefined") return;

  const conn = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/forum")
    .withAutomaticReconnect()
    .build();

  // Thông báo cá nhân -> rung chuông + cập nhật badge.
  conn.on("notify", data => {
    if (Forum.notifyNew) Forum.notifyNew(data.count);
    else { const b = document.getElementById("notif-badge"); if (b) { b.textContent = data.count; b.classList.remove("hide"); } }
  });

  // Chủ đề mới đẩy lên board (chỉ hiện banner trên trang chủ/danh sách).
  let newCount = 0;
  conn.on("newTopic", topic => {
    const list = document.querySelector(".topic-list");
    if (!list || location.pathname.startsWith("/chu-de")) return;
    newCount++;
    showBanner(topic);
  });

  /* ---- Chat chung (shoutbox) ---- */
  conn.on("shout", s => {
    const list = document.getElementById("shoutbox-msgs");
    if (!list) return;
    if (list.querySelector(`.shout[data-id="${s.id}"]`)) return;
    const empty = list.querySelector(".muted.small.text-center"); if (empty) empty.remove();
    const el = document.createElement("div");
    el.className = "shout highlight";
    el.dataset.id = s.id;
    el.innerHTML = `${shoutAvatar(s.avatar, s.name)}<div class="shout-body">` +
      `<a class="user-link shout-name" data-username="${esc(s.username)}" href="/thanh-vien/${esc(s.username)}">${esc(s.name)}</a>` +
      `<span class="shout-text"></span><span class="shout-time">vừa xong</span></div>`;
    el.querySelector(".shout-text").textContent = s.body;
    list.appendChild(el);
    list.scrollTop = list.scrollHeight;
  });

  function shoutAvatar(url, name) {
    if (url) return `<img class="avatar avatar-24" src="${esc(url)}" alt="" width="24" height="24">`;
    const p = (name || "?").trim().split(/\s+/);
    const ini = (p.length === 1 ? p[0].slice(0, 2) : p[p.length - 2][0] + p[p.length - 1][0]).toUpperCase();
    return `<span class="avatar avatar-24" style="width:24px;height:24px;background:#7c7c7c;color:#fff;display:inline-flex;align-items:center;justify-content:center;font-weight:700;font-size:10px;border-radius:50%;flex:none;">${esc(ini)}</span>`;
  }
  function esc(s) { return (window.Forum && Forum.escapeHtml) ? Forum.escapeHtml(s) : String(s == null ? "" : s); }

  document.addEventListener("DOMContentLoaded", () => {
    const list = document.getElementById("shoutbox-msgs");
    if (list) list.scrollTop = list.scrollHeight;
    const sForm = document.getElementById("shoutbox-form");
    if (sForm) {
      sForm.addEventListener("submit", async e => {
        e.preventDefault();
        const input = document.getElementById("shoutbox-input");
        const body = input.value.trim();
        if (!body) return;
        if (conn.state === signalR.HubConnectionState.Connected) {
          try { await conn.invoke("SendShout", body); input.value = ""; }
          catch { Forum.toast("Không gửi được tin.", "error"); }
        } else { Forum.toast("Đang kết nối lại, thử lại sau giây lát…", "warning"); }
      });
    }
  });

  function showBanner(topic) {
    let banner = document.getElementById("new-topic-banner");
    if (!banner) {
      banner = document.createElement("a");
      banner.id = "new-topic-banner";
      banner.href = location.pathname + location.search;
      banner.className = "btn btn-primary cta-pulse";
      banner.style.cssText = "position:sticky;top:60px;z-index:50;display:flex;justify-content:center;margin:0 auto 10px;width:max-content;";
      const main = document.querySelector(".main-col");
      if (main) main.insertBefore(banner, main.firstChild);
      banner.addEventListener("click", () => sessionStorage.setItem("scrollTop", "1"));
    }
    banner.innerHTML = `🔵 ${newCount} chủ đề mới — bấm để xem`;
    banner.classList.remove("badge-pop"); void banner.offsetWidth; banner.classList.add("badge-pop");
  }

  conn.start().catch(() => { /* im lặng nếu không kết nối được */ });
})();
