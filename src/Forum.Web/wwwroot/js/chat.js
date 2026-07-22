/* =========================================================================
   Chat trang đầy đủ (/tin-nhan) qua SignalR: gửi/nhận text + đính kèm
   (ảnh dán clipboard, file pdf/docx/excel), xem ảnh khi click (lightbox toàn cục).
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};
  if (typeof signalR === "undefined") { console.warn("SignalR chưa tải"); return; }

  const msgs = document.getElementById("chat-msgs");
  const form = document.getElementById("chat-form");
  const me = msgs ? parseInt(msgs.dataset.me, 10) : 0;

  const conn = new signalR.HubConnectionBuilder().withUrl("/hubs/chat").withAutomaticReconnect().build();

  function fmtTime(iso) {
    const d = new Date(iso);
    return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
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

  function appendMsg(m) {
    if (!msgs) return;
    if (parseInt(msgs.dataset.conversation, 10) !== m.conversationId) return;
    if (msgs.querySelector(`[data-msg-id="${m.id}"]`)) return;
    const el = document.createElement("div");
    el.className = "msg" + (m.senderId === me ? " mine" : "");
    el.dataset.msgId = m.id;
    if (m.body) { const t = document.createElement("div"); t.textContent = m.body; el.appendChild(t); }
    if (m.attachmentUrl) el.appendChild(attachmentNode(m));
    const time = document.createElement("span"); time.className = "t"; time.textContent = fmtTime(m.createdAt);
    el.appendChild(time);
    msgs.appendChild(el);
    msgs.scrollTop = msgs.scrollHeight;
  }

  conn.on("message", appendMsg);
  conn.on("presence", p => {
    document.querySelectorAll(`[data-presence="${p.userId}"]`).forEach(d => d.classList.toggle("online", p.online));
    document.querySelectorAll(`[data-presence-label="${p.userId}"]`).forEach(l => l.textContent = p.online ? "Đang hoạt động" : "Ngoại tuyến");
  });
  conn.start().then(() => { if (msgs) msgs.scrollTop = msgs.scrollHeight; }).catch(err => console.warn("Chat hub:", err));

  async function send(convId, body, attach) {
    if (conn.state !== signalR.HubConnectionState.Connected) { Forum.toast("Đang kết nối lại…", "warning"); return false; }
    try {
      await conn.invoke("SendMessage", convId, body || "",
        attach ? attach.url : null, attach ? attach.name : null, attach ? attach.type : null, attach ? attach.isImage : false);
      return true;
    } catch { Forum.toast("Không gửi được tin nhắn.", "error"); return false; }
  }
  async function uploadFile(blob, filename) {
    const fd = new FormData();
    fd.append("file", blob, filename || blob.name || "file");
    return await Forum.api.postForm("/tin-nhan/tai-len", fd);
  }

  if (form) {
    const input = document.getElementById("chat-input");
    const convId = parseInt(form.dataset.conversation, 10);
    const fileInput = document.getElementById("chat-file");

    form.addEventListener("submit", async e => {
      e.preventDefault();
      const body = input.value.trim();
      if (!body) return;
      if (await send(convId, body)) { input.value = ""; input.focus(); }
    });

    document.getElementById("chat-attach")?.addEventListener("click", () => fileInput.click());
    fileInput?.addEventListener("change", async () => {
      const f = fileInput.files[0];
      if (!f) return;
      try { const r = await uploadFile(f); if (await send(convId, input.value.trim(), r)) input.value = ""; }
      catch (err) { Forum.toast(err.message || "Tải lên thất bại.", "error"); }
      finally { fileInput.value = ""; }
    });
    input.addEventListener("paste", async e => {
      const items = e.clipboardData && e.clipboardData.items;
      if (!items) return;
      for (const it of items) {
        if (it.type && it.type.indexOf("image/") === 0) {
          e.preventDefault();
          const blob = it.getAsFile();
          if (!blob) return;
          try { const r = await uploadFile(blob, `paste-${Date.now()}.png`); if (await send(convId, input.value.trim(), r)) input.value = ""; }
          catch (err) { Forum.toast(err.message || "Dán ảnh thất bại.", "error"); }
          return;
        }
      }
    });
  }
})();
