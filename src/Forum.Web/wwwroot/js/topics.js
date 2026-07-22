/* =========================================================================
   Tương tác trên trang chủ đề: lưu, theo dõi, chia sẻ, xóa, báo cáo, poll, kiểm duyệt.
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};

  document.addEventListener("click", async e => {
    // ---- Subscribe ----
    const sub = e.target.closest("[data-subscribe]");
    if (sub) {
      e.preventDefault();
      if (!Forum.isAuthenticated) { Forum.toast("Đăng nhập để theo dõi.", "warning"); return; }
      try {
        const r = await Forum.api.post(`/chu-de/${sub.dataset.subscribe}/theo-doi`, {});
        sub.classList.toggle("btn-primary", r.subscribed);
        const lbl = sub.querySelector(".sub-label"); if (lbl) lbl.textContent = r.subscribed ? "Đang theo dõi" : "Theo dõi";
        Forum.toast(r.subscribed ? "Đã theo dõi chủ đề." : "Đã bỏ theo dõi.", "success", 1500);
      } catch (err) { Forum.toast(err.message || "Lỗi.", "error"); }
      return;
    }

    // ---- Share ----
    const share = e.target.closest("[data-share]");
    if (share) {
      e.preventDefault();
      const url = share.dataset.share;
      try { await navigator.clipboard.writeText(url); Forum.toast("Đã sao chép liên kết.", "success", 1500); }
      catch { Forum.toast(url, "info"); }
      return;
    }

    // ---- Delete topic ----
    const del = e.target.closest("[data-delete-topic]");
    if (del) {
      e.preventDefault();
      if (!await Forum.confirm({ title: "Xóa chủ đề", message: "Bạn chắc chắn muốn xóa chủ đề này?" })) return;
      try {
        const r = await Forum.api.post(`/chu-de/${del.dataset.deleteTopic}/xoa`, {});
        if (r.ok) { Forum.toast("Đã xóa chủ đề.", "success", 1500); setTimeout(() => location.href = "/", 600); }
      } catch (err) { Forum.toast(err.message || "Không xóa được.", "error"); }
      return;
    }

    // ---- Report ----
    const rep = e.target.closest("[data-report]");
    if (rep) { e.preventDefault(); openReport(rep.dataset.report, rep.dataset.id); return; }

    // ---- Poll vote ----
    const opt = e.target.closest(".poll-opt");
    if (opt) {
      e.preventDefault();
      const poll = opt.closest("[data-poll]");
      if (poll.dataset.voted === "true") { Forum.toast("Bạn đã bình chọn.", "info", 1500); return; }
      if (!Forum.isAuthenticated) { Forum.toast("Đăng nhập để bình chọn.", "warning"); return; }
      try {
        const r = await Forum.api.post("/binh-chon/vote", { optionId: parseInt(opt.dataset.option, 10) });
        applyPoll(poll, r);
        Forum.toast("Đã ghi nhận bình chọn.", "success", 1500);
      } catch (err) { Forum.toast(err.message || "Lỗi.", "error"); }
      return;
    }

    // ---- Moderation ----
    const mod = e.target.closest("[data-mod]");
    if (mod) {
      e.preventDefault();
      const map = { pin: "/kiem-duyet/ghim", lock: "/kiem-duyet/khoa", feature: "/kiem-duyet/noi-bat" };
      try {
        await Forum.api.post(map[mod.dataset.mod], { id: parseInt(mod.dataset.id, 10), on: mod.dataset.on !== "true" });
        Forum.toast("Đã cập nhật.", "success", 1200);
        setTimeout(() => location.reload(), 500);
      } catch (err) { Forum.toast(err.message || "Lỗi.", "error"); }
      return;
    }
  });

  function applyPoll(poll, r) {
    poll.dataset.voted = "true";
    (r.options || []).forEach(o => {
      const el = poll.querySelector(`.poll-opt[data-option="${o.id}"]`);
      if (!el) return;
      const pct = r.total > 0 ? Math.round(o.voteCount * 100 / r.total) : 0;
      el.querySelector(".bar").style.width = pct + "%";
      const p = el.querySelector(".pct"); p.style.display = ""; p.textContent = `${pct}% · ${o.voteCount}`;
      if (o.id === r.votedOptionId) el.classList.add("voted");
    });
    const total = poll.querySelector(".poll-total"); if (total) total.textContent = r.total;
  }

  function openReport(type, id) {
    if (!Forum.isAuthenticated) { Forum.toast("Đăng nhập để báo cáo.", "warning"); return; }
    const reasons = ["Spam / Quảng cáo", "Nội dung không phù hợp", "Quấy rối / Xúc phạm", "Sai danh mục", "Thông tin sai lệch", "Khác"];
    const m = Forum.modal(`<h3>Báo cáo nội dung</h3>
      <div class="field"><label class="form-label">Lý do</label>
        <select class="form-control" id="rep-reason">${reasons.map(r => `<option>${r}</option>`).join("")}</select></div>
      <div class="field"><label class="form-label">Chi tiết (tuỳ chọn)</label>
        <textarea class="form-control" id="rep-details" rows="3" placeholder="Mô tả thêm…"></textarea></div>
      <div class="modal-actions"><button class="btn" data-act="cancel">Hủy</button><button class="btn btn-primary" data-act="ok">Gửi báo cáo</button></div>`);
    m.el.querySelector('[data-act="cancel"]').addEventListener("click", m.close);
    m.el.querySelector('[data-act="ok"]').addEventListener("click", async () => {
      try {
        await Forum.api.post("/bao-cao", { type, id: parseInt(id, 10), reason: m.el.querySelector("#rep-reason").value, details: m.el.querySelector("#rep-details").value });
        m.close(); Forum.toast("Đã gửi báo cáo. Cảm ơn bạn!", "success");
      } catch (err) { Forum.toast(err.message || "Không gửi được.", "error"); }
    });
  }
})();
