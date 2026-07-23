/* Tương tác: thả cảm xúc (emoji), chọn đáp án hay, theo dõi thẻ. */
(function () {
  "use strict";
  var Forum = window.Forum;
  if (!Forum) return;

  document.addEventListener("click", async function (e) {
    // ---- Thả cảm xúc ----
    var rb = e.target.closest("[data-reactions] .reaction");
    if (rb) {
      e.preventDefault();
      if (rb.disabled) return;
      var wrap = rb.closest("[data-reactions]");
      if (!Forum.isAuthenticated) { Forum.toast("Đăng nhập để thả cảm xúc.", "warning"); return; }
      var emoji = rb.getAttribute("data-emoji");
      var isComment = wrap.getAttribute("data-target") === "comment";
      var id = parseInt(wrap.getAttribute("data-id"), 10);
      try {
        var r = await Forum.api.post("/tuong-tac/cam-xuc", { isComment: isComment, id: id, emoji: emoji });
        var mine = r.mine || [];
        wrap.querySelectorAll(".reaction").forEach(function (b) {
          var em = b.getAttribute("data-emoji");
          var cnt = (r.counts && r.counts[em]) || 0;
          var cEl = b.querySelector(".r-count");
          if (cEl) { cEl.textContent = cnt; cEl.style.display = cnt > 0 ? "" : "none"; }
          b.classList.toggle("on", mine.indexOf(em) >= 0);
        });
      } catch (ex) { Forum.toast(ex.message || "Không thực hiện được.", "error"); }
      return;
    }

    // ---- Chọn / bỏ chọn đáp án hay ----
    var ab = e.target.closest("[data-accept]");
    if (ab) {
      e.preventDefault();
      var cid = parseInt(ab.getAttribute("data-accept"), 10);
      var tid = parseInt(ab.getAttribute("data-topic"), 10);
      try {
        var r2 = await Forum.api.post("/tuong-tac/dap-an", { topicId: tid, commentId: cid });
        if (r2.ok) { location.reload(); } else { Forum.toast("Không thực hiện được.", "error"); }
      } catch (ex) { Forum.toast(ex.message || "Không thực hiện được.", "error"); }
      return;
    }

    // ---- Theo dõi thẻ ----
    var tf = e.target.closest("[data-tag-follow]");
    if (tf) {
      e.preventDefault();
      if (!Forum.isAuthenticated) { Forum.toast("Đăng nhập để theo dõi thẻ.", "warning"); return; }
      var tagId = parseInt(tf.getAttribute("data-tag-follow"), 10);
      try {
        var r3 = await Forum.api.post("/tuong-tac/the/theo-doi", { tagId: tagId });
        tf.classList.toggle("on", r3.subscribed);
        var lbl = tf.querySelector("[data-follow-label]");
        if (lbl) lbl.textContent = r3.subscribed ? "Đang theo dõi" : "Theo dõi thẻ";
        Forum.toast(r3.subscribed ? "Đã theo dõi thẻ." : "Đã bỏ theo dõi thẻ.", "success", 1400);
      } catch (ex) { Forum.toast(ex.message || "Không thực hiện được.", "error"); }
      return;
    }

    // ---- Trích dẫn bình luận vào ô soạn ----
    var q = e.target.closest("[data-quote]");
    if (q) {
      e.preventDefault();
      var cid = q.getAttribute("data-quote");
      var body = document.querySelector("#comment-" + cid + " .comment-body");
      var ta = document.querySelector("[data-comment-form] textarea[name=body]");
      if (body && ta) {
        var text = body.innerText.trim().split("\n").map(function (l) { return "> " + l; }).join("\n");
        ta.value = (ta.value ? ta.value + "\n\n" : "") + text + "\n\n";
        ta.focus();
        ta.scrollIntoView({ behavior: "smooth", block: "center" });
        Forum.toast("Đã trích dẫn vào ô bình luận.", "info", 1400);
      }
      return;
    }

    // ---- Duyệt / từ chối hàng loạt (bảng kiểm duyệt) ----
    var bulkAll = e.target.closest("[data-bulk-all]");
    if (bulkAll) {
      var sc = bulkAll.closest("[data-bulk-scope]");
      sc.querySelectorAll(".bulk-chk").forEach(function (c) { c.checked = bulkAll.checked; });
      return;
    }
    var bulkBtn = e.target.closest("[data-bulk-approve],[data-bulk-reject]");
    if (bulkBtn) {
      e.preventDefault();
      var isApprove = bulkBtn.hasAttribute("data-bulk-approve");
      var scope = bulkBtn.closest("[data-bulk-scope]");
      var ids = Array.prototype.map.call(scope.querySelectorAll(".bulk-chk:checked"), function (c) { return parseInt(c.value, 10); });
      if (ids.length === 0) { Forum.toast("Chưa chọn mục nào.", "warning"); return; }
      var reason = isApprove ? null : (prompt("Lý do từ chối (áp cho tất cả):") || "");
      try {
        var rb = await Forum.api.post(isApprove ? "/kiem-duyet/duyet-nhieu" : "/kiem-duyet/tu-choi-nhieu", { ids: ids, reason: reason });
        Forum.toast("Đã xử lý " + rb.count + " bài.", "success");
        location.reload();
      } catch (ex) { Forum.toast(ex.message || "Lỗi.", "error"); }
      return;
    }

    // ---- Chặn / bỏ chặn thành viên ----
    var blk = e.target.closest("[data-block-user]");
    if (blk) {
      e.preventDefault();
      if (!Forum.isAuthenticated) { Forum.toast("Đăng nhập để chặn.", "warning"); return; }
      try {
        var rbk = await Forum.api.post("/tuong-tac/chan", { userId: parseInt(blk.getAttribute("data-block-user"), 10) });
        blk.classList.toggle("btn-danger", rbk.blocked);
        var bl = blk.querySelector("[data-block-label]"); if (bl) bl.textContent = rbk.blocked ? "Bỏ chặn" : "Chặn";
        Forum.toast(rbk.blocked ? "Đã chặn thành viên." : "Đã bỏ chặn.", "success", 1500);
      } catch (ex) { Forum.toast(ex.message || "Không thực hiện được.", "error"); }
      return;
    }

    // ---- Công cụ điều hành trên hồ sơ (warn / mute / notes) ----
    var panel = e.target.closest("[data-modtools]");
    if (!panel) return;
    var uid = parseInt(panel.getAttribute("data-user"), 10);

    async function warn(reason) {
      if (!reason) return;
      try {
        var r = await Forum.api.post("/kiem-duyet/canh-cao", { id: uid, reason: reason });
        Forum.toast(r.ok ? "Đã gửi cảnh cáo." : (r.error || "Không thực hiện được."), r.ok ? "success" : "error");
      } catch (ex) { Forum.toast(ex.message || "Lỗi.", "error"); }
    }

    var warnBtn = e.target.closest("[data-mt-warn]");
    if (warnBtn) { e.preventDefault(); var rs = prompt("Lý do cảnh cáo:"); if (rs && rs.trim()) warn(rs.trim()); return; }

    var chip = e.target.closest("[data-mt-canned]");
    if (chip) { e.preventDefault(); warn(chip.getAttribute("data-mt-canned")); return; }

    var muteBtn = e.target.closest("[data-mt-mute]");
    if (muteBtn) {
      e.preventDefault();
      var hours = parseInt(panel.querySelector("[data-mt-hours]").value, 10) || 24;
      var reason = prompt("Lý do tạm cấm (tuỳ chọn):") || "";
      try {
        var r = await Forum.api.post("/kiem-duyet/tam-cam", { id: uid, hours: hours, reason: reason });
        if (r.ok) { Forum.toast("Đã tạm cấm nói.", "success"); location.reload(); }
        else Forum.toast(r.error || "Không thực hiện được.", "error");
      } catch (ex) { Forum.toast(ex.message || "Lỗi.", "error"); }
      return;
    }

    var unmuteBtn = e.target.closest("[data-mt-unmute]");
    if (unmuteBtn) {
      e.preventDefault();
      try {
        await Forum.api.post("/kiem-duyet/tam-cam", { id: uid, hours: 0, reason: "" });
        Forum.toast("Đã bỏ cấm nói.", "success"); location.reload();
      } catch (ex) { Forum.toast(ex.message || "Lỗi.", "error"); }
      return;
    }

    var noteAdd = e.target.closest("[data-mt-note-add]");
    if (noteAdd) {
      e.preventDefault();
      var input = panel.querySelector("[data-mt-note]");
      var body = (input.value || "").trim();
      if (!body) return;
      try {
        var r = await Forum.api.post("/kiem-duyet/ghi-chu", { userId: uid, body: body });
        if (r.ok) { Forum.toast("Đã lưu ghi chú.", "success"); location.reload(); }
        else Forum.toast(r.error || "Không thực hiện được.", "error");
      } catch (ex) { Forum.toast(ex.message || "Lỗi.", "error"); }
      return;
    }

    var noteDel = e.target.closest("[data-mt-note-del]");
    if (noteDel) {
      e.preventDefault();
      try {
        await Forum.api.post("/kiem-duyet/ghi-chu/xoa", { id: parseInt(noteDel.getAttribute("data-mt-note-del"), 10) });
        var li = noteDel.closest("[data-note]"); if (li) li.remove();
      } catch (ex) { Forum.toast(ex.message || "Lỗi.", "error"); }
      return;
    }
  });
})();
