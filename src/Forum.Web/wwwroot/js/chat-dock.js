/* =========================================================================
   Chat dock kiểu Messenger với hàng chờ:
   - Tối đa 3 CỬA SỔ active mở cùng lúc (dàn ngang bên trái).
   - Hàng chờ (chat head avatar) tối đa 5; mở cái thứ 4 thì cửa sổ cũ nhất tụt
     xuống hàng chờ; khi hàng chờ vượt 5 thì đẩy avatar cũ nhất ra (FIFO).
   - Thu nhỏ cửa sổ -> về avatar (hàng chờ). Bấm avatar -> bung lại thành cửa sổ.
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};
  if (!Forum.isAuthenticated) return;

  const dock = document.getElementById("chat-dock");
  const winArea = document.getElementById("chat-windows");
  const rail = document.getElementById("chat-rail");
  const launcher = document.getElementById("chat-launcher");
  if (!dock || !winArea || !rail || !launcher) return;

  const MAX_ACTIVE = 3;
  const MAX_WAITING = 5;

  const convs = new Map();      // id -> { meta, win, head }
  const activeOrder = [];       // id đang mở cửa sổ (cũ -> mới)
  const waitingOrder = [];      // id đang ở hàng chờ (cũ -> mới, FIFO)
  let listPanel = null, me = null, unread = 0, conn = null;

  if (typeof signalR !== "undefined") {
    conn = new signalR.HubConnectionBuilder().withUrl("/hubs/chat").withAutomaticReconnect().build();
    conn.on("message", onMessage);
    conn.on("presence", onPresence);
    conn.start().catch(() => {});
  }

  launcher.addEventListener("click", toggleList);

  /* ---- Danh sách hội thoại ---- */
  async function toggleList() {
    if (listPanel) { closeList(); return; }
    listPanel = document.createElement("div");
    listPanel.className = "chat-list-panel";
    listPanel.innerHTML = `<div class="head"><span>Đoạn chat</span><a href="/tin-nhan" class="small" data-no-progress>Mở Messenger</a></div>
      <div class="body"><div class="card-pad muted small">Đang tải…</div></div>`;
    winArea.appendChild(listPanel);
    clearBadge();
    try {
      const list = await Forum.api.get("/tin-nhan/danh-sach");
      const body = listPanel.querySelector(".body");
      if (!list.length) { body.innerHTML = `<div class="card-pad muted small">Chưa có cuộc trò chuyện nào.</div>`; return; }
      body.innerHTML = "";
      list.forEach(c => body.appendChild(listItem(c)));
    } catch {
      if (listPanel) listPanel.querySelector(".body").innerHTML = `<div class="card-pad muted small">Không tải được.</div>`;
    }
  }
  function closeList() { if (listPanel) { listPanel.remove(); listPanel = null; } }

  function listItem(c) {
    const el = document.createElement("div");
    el.className = "chat-list-item" + (c.unread ? " unread" : "");
    el.innerHTML = `${avatar(c.avatar, c.name, 36, c.otherId, c.online)}
      <div style="flex:1;min-width:0;"><div class="nm">${esc(c.name)}</div>
        <div class="pv">${c.lastMine ? "Bạn: " : ""}${esc(c.lastMessage || "Bắt đầu trò chuyện")}</div></div>`;
    el.addEventListener("click", () => { openConversation(c); closeList(); });
    return el;
  }

  /* ---- Mô hình hàng đợi ---- */
  function rememberMeta(meta) {
    if (convs.has(meta.id)) Object.assign(convs.get(meta.id).meta, meta);
    else convs.set(meta.id, { meta: Object.assign({}, meta), win: null, head: null });
  }

  function openConversation(meta) {
    rememberMeta(meta);
    const id = meta.id;
    if (activeOrder.includes(id)) {            // đang mở -> đưa lên gần nhất + focus
      moveToEnd(activeOrder, id);
      convs.get(id).win?.querySelector("input")?.focus();
      return;
    }
    if (waitingOrder.includes(id)) removeFromWaiting(id);
    addActive(id);
  }

  function addActive(id) {
    while (activeOrder.length >= MAX_ACTIVE) demoteToWaiting(activeOrder.shift());
    activeOrder.push(id);
    buildWindow(id);
  }

  function demoteToWaiting(id) {
    const rec = convs.get(id);
    if (!rec) return;
    if (rec.win) { rec.win.remove(); rec.win = null; }
    while (waitingOrder.length >= MAX_WAITING) closeConv(waitingOrder.shift()); // FIFO đẩy cái cũ nhất
    waitingOrder.push(id);
    buildHead(id);
  }

  function minimize(id) {
    const i = activeOrder.indexOf(id);
    if (i >= 0) activeOrder.splice(i, 1);
    demoteToWaiting(id);
  }

  function removeFromWaiting(id) {
    const i = waitingOrder.indexOf(id);
    if (i >= 0) waitingOrder.splice(i, 1);
    const rec = convs.get(id);
    if (rec?.head) { rec.head.remove(); rec.head = null; }
  }

  function closeConv(id) {
    const rec = convs.get(id);
    if (!rec) return;
    rec.win?.remove();
    rec.head?.remove();
    let i = activeOrder.indexOf(id); if (i >= 0) activeOrder.splice(i, 1);
    i = waitingOrder.indexOf(id); if (i >= 0) waitingOrder.splice(i, 1);
    convs.delete(id);
  }

  function moveToEnd(arr, id) { const i = arr.indexOf(id); if (i >= 0) { arr.splice(i, 1); arr.push(id); } }

  function buildHead(id) {
    const rec = convs.get(id);
    const head = document.createElement("button");
    head.type = "button"; head.className = "chat-head"; head.title = rec.meta.name;
    head.innerHTML = `${avatar(rec.meta.avatar, rec.meta.name, 48, rec.meta.otherId, rec.meta.online)}<span class="chat-head-unread hide"></span>`;
    head.addEventListener("click", () => openConversation(rec.meta));
    rail.insertBefore(head, launcher);
    rec.head = head;
  }

  async function buildWindow(id) {
    const rec = convs.get(id);
    const win = document.createElement("div");
    win.className = "chat-window";
    win.innerHTML = `
      <div class="cw-head">
        <span class="cw-av"></span>
        <span class="nm">${esc(rec.meta.name || "…")}</span>
        <span class="acts"><button type="button" data-min title="Thu nhỏ">—</button><button type="button" data-close title="Đóng">✕</button></span>
      </div>
      <div class="cw-body"><div class="muted small">Đang tải…</div></div>
      <form class="cw-compose">
        <button type="button" class="cw-attach" title="Đính kèm ảnh/tệp" aria-label="Đính kèm">📎</button>
        <input type="file" class="cw-file" accept="image/*,.pdf,.doc,.docx,.xls,.xlsx" hidden>
        <input type="text" class="cw-text" placeholder="Aa (Ctrl+V để dán ảnh)" autocomplete="off" maxlength="4000">
        <button type="submit" aria-label="Gửi">${sendIcon()}</button>
      </form>`;
    winArea.appendChild(win);
    rec.win = win;

    const msgsEl = win.querySelector(".cw-body");
    const input = win.querySelector(".cw-text");
    const fileInput = win.querySelector(".cw-file");
    win.querySelector(".cw-av").innerHTML = avatar(rec.meta.avatar, rec.meta.name, 28, rec.meta.otherId, rec.meta.online);
    win.querySelector("[data-min]").addEventListener("click", () => minimize(id));
    win.querySelector("[data-close]").addEventListener("click", () => closeConv(id));

    win.querySelector(".cw-compose").addEventListener("submit", async e => {
      e.preventDefault();
      const v = input.value.trim();
      if (!v) return;
      if (await sendMessage(id, v)) input.value = "";
    });

    // Đính kèm ảnh/tệp.
    win.querySelector(".cw-attach").addEventListener("click", () => fileInput.click());
    fileInput.addEventListener("change", async () => {
      const f = fileInput.files[0];
      if (!f) return;
      try { const r = await uploadChatFile(f); if (await sendMessage(id, input.value.trim(), r)) input.value = ""; }
      catch (err) { Forum.toast(err.message || "Tải lên thất bại.", "error"); }
      finally { fileInput.value = ""; }
    });

    // Dán ảnh từ clipboard (Ctrl+V).
    input.addEventListener("paste", async e => {
      const items = e.clipboardData && e.clipboardData.items;
      if (!items) return;
      for (const it of items) {
        if (it.type && it.type.indexOf("image/") === 0) {
          e.preventDefault();
          const blob = it.getAsFile();
          if (!blob) return;
          try { const r = await uploadChatFile(blob, `paste-${Date.now()}.png`); if (await sendMessage(id, input.value.trim(), r)) input.value = ""; }
          catch (err) { Forum.toast(err.message || "Dán ảnh thất bại.", "error"); }
          return;
        }
      }
    });

    try {
      const data = await Forum.api.get(`/tin-nhan/${id}/tin`);
      me = data.me;
      Object.assign(rec.meta, { name: data.name, avatar: data.avatar, otherId: data.otherId, online: data.online });
      win.querySelector(".nm").textContent = data.name;
      win.querySelector(".cw-av").innerHTML = avatar(data.avatar, data.name, 28, data.otherId, data.online);
      msgsEl.innerHTML = "";
      data.messages.forEach(m => msgsEl.appendChild(bubble(m)));
      msgsEl.scrollTop = msgsEl.scrollHeight;
      input.focus();
    } catch {
      msgsEl.innerHTML = `<div class="muted small">Không tải được hội thoại.</div>`;
    }
  }

  function bubble(m) {
    const el = document.createElement("div");
    el.className = "cw-msg" + (m.senderId === me ? " mine" : "");
    el.dataset.id = m.id;
    if (m.body) { const t = document.createElement("div"); t.textContent = m.body; el.appendChild(t); }
    if (m.attachmentUrl) el.appendChild(attachmentNode(m));
    return el;
  }

  function attachmentNode(m) {
    if (m.isImage) {
      const img = document.createElement("img");
      img.className = "chat-img"; img.src = m.attachmentUrl; img.alt = m.attachmentName || "ảnh"; img.loading = "lazy";
      return img;
    }
    const a = document.createElement("a");
    a.className = "chat-file"; a.href = m.attachmentUrl; a.target = "_blank"; a.rel = "noopener";
    a.setAttribute("download", m.attachmentName || "");
    a.innerHTML = `<span class="cf-ico">📄</span><span class="cf-name"></span>`;
    a.querySelector(".cf-name").textContent = m.attachmentName || "Tệp đính kèm";
    return a;
  }

  async function uploadChatFile(blob, filename) {
    const fd = new FormData();
    fd.append("file", blob, filename || blob.name || "file");
    return await Forum.api.postForm("/tin-nhan/tai-len", fd); // {url,name,type,isImage}
  }

  async function sendMessage(convId, body, attach) {
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) { Forum.toast("Đang kết nối lại, thử lại sau giây lát…", "warning"); return false; }
    try {
      await conn.invoke("SendMessage", convId, body || "",
        attach ? attach.url : null, attach ? attach.name : null, attach ? attach.type : null, attach ? attach.isImage : false);
      return true;
    } catch { Forum.toast("Không gửi được tin nhắn.", "error"); return false; }
  }

  function onMessage(m) {
    const rec = convs.get(m.conversationId);
    if (rec?.win && activeOrder.includes(m.conversationId)) {
      const body = rec.win.querySelector(".cw-body");
      if (body.querySelector(`[data-id="${m.id}"]`)) return;
      body.appendChild(bubble(m)); // m gồm cả attachmentUrl/isImage…
      body.scrollTop = body.scrollHeight;
    } else if (rec?.head) {
      if (m.senderId !== me) setHeadUnread(rec, true);
    } else if (m.senderId !== me) {
      bumpBadge();
    }
  }

  function onPresence(p) {
    document.querySelectorAll(`.presence-dot[data-presence="${p.userId}"]`).forEach(d => d.classList.toggle("online", p.online));
  }

  function setHeadUnread(rec, on) { rec.head?.querySelector(".chat-head-unread")?.classList.toggle("hide", !on); }

  function bumpBadge() {
    unread++;
    const b = document.getElementById("chat-launcher-badge");
    if (b) { b.textContent = unread > 9 ? "9+" : unread; b.classList.remove("hide"); }
    launcher.classList.remove("bell-shake"); void launcher.offsetWidth; launcher.classList.add("bell-shake");
  }
  function clearBadge() { unread = 0; document.getElementById("chat-launcher-badge")?.classList.add("hide"); }

  // Mở chat với 1 người từ nút "Nhắn tin".
  document.addEventListener("click", async e => {
    const t = e.target.closest("[data-chat-with]");
    if (!t) return;
    e.preventDefault();
    try {
      const r = await Forum.api.post(`/tin-nhan/voi-nguoi/${encodeURIComponent(t.dataset.chatWith)}`, {});
      openConversation({ id: r.conversationId, name: r.name, avatar: r.avatar, otherId: r.otherId, online: r.online });
    } catch (err) { Forum.toast(err.message || "Không mở được hội thoại.", "error"); }
  });

  /* ---- helpers ---- */
  function avatar(url, name, size, userId, online) {
    const dot = (online !== undefined)
      ? `<span class="presence-dot ${online ? "online" : ""}" data-presence="${userId}" style="position:absolute;bottom:0;right:0;border:2px solid var(--surface);"></span>` : "";
    const inner = url
      ? `<img class="avatar avatar-${size}" src="${esc(url)}" alt="" width="${size}" height="${size}">`
      : initials(name, size);
    return `<span style="position:relative;display:inline-block;flex:none;">${inner}${dot}</span>`;
  }
  function initials(name, size) {
    const parts = (name || "?").trim().split(/\s+/);
    const ini = (parts.length === 1 ? parts[0].slice(0, 2) : parts[parts.length - 2][0] + parts[parts.length - 1][0]).toUpperCase();
    const fs = Math.max(9, Math.round(size * 0.42));
    return `<span class="avatar avatar-${size}" style="width:${size}px;height:${size}px;background:#7c7c7c;color:#fff;display:inline-flex;align-items:center;justify-content:center;font-weight:700;font-size:${fs}px;border-radius:50%;">${esc(ini)}</span>`;
  }
  function sendIcon() {
    return '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.536 21.686a.5.5 0 0 0 .937-.024l6.5-19a.496.496 0 0 0-.635-.635l-19 6.5a.5.5 0 0 0-.024.937l7.93 3.18a2 2 0 0 1 1.112 1.11z"/><path d="m21.854 2.147-10.94 10.939"/></svg>';
  }
  function esc(s) { return Forum.escapeHtml(s); }
})();
