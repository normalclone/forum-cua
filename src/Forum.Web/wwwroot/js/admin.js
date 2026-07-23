/* =========================================================================
   Admin (/quan-tri): thao tác AJAX cho người dùng, danh mục, thẻ.
   ========================================================================= */
(function () {
  "use strict";
  var Forum = window.Forum;
  if (!Forum) return;

  function err(e) { Forum.toast((e && e.message) || "Không thực hiện được.", "error"); }

  // Xóa logo/favicon đã chọn.
  document.addEventListener("click", function (e) {
    var clr = e.target.closest("[data-clear-target]");
    if (!clr) return;
    e.preventDefault();
    var t = clr.getAttribute("data-clear-target");
    var h = document.getElementById(t); if (h) h.value = "";
    var img = document.querySelector('[data-preview="' + t + '"]'); if (img) img.style.display = "none";
  });

  document.addEventListener("change", async function (e) {
    // Đổi vai trò người dùng.
    var sel = e.target.closest("select[data-set-role]");
    if (sel) {
      var id = parseInt(sel.getAttribute("data-set-role"), 10);
      try {
        var r = await Forum.api.post("/quan-tri/nguoi-dung/vai-tro", { id: id, role: sel.value });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        var row = sel.closest("[data-user-row]");
        var badge = row && row.querySelector("[data-role-badge]");
        if (badge) { badge.textContent = r.role; badge.className = "role-badge r-" + r.role; }
        Forum.toast("Đã đổi vai trò → " + r.role, "success", 1600);
      } catch (ex) { err(ex); }
    }

    // Chọn tất cả chủ đề (quản lý nội dung).
    var chkAll = e.target.closest("#chk-all");
    if (chkAll) {
      document.querySelectorAll(".topic-chk").forEach(function (c) { c.checked = chkAll.checked; });
    }

    // Tải ảnh thương hiệu (logo/favicon) → điền vào input ẩn + preview.
    var up = e.target.closest("[data-upload-target]");
    if (up && up.files && up.files[0]) {
      var target = up.getAttribute("data-upload-target");
      var fd = new FormData(); fd.append("file", up.files[0]);
      try {
        var r = await Forum.api.postForm("/quan-tri/tai-anh", fd);
        var hidden = document.getElementById(target);
        if (hidden) hidden.value = r.url;
        var img = document.querySelector('[data-preview="' + target + '"]');
        if (img) { img.src = r.url; img.style.display = ""; }
        Forum.toast("Đã tải ảnh lên.", "success", 1500);
      } catch (ex) { Forum.toast(ex.message || "Tải ảnh thất bại.", "error"); }
    }
  });

  document.addEventListener("click", async function (e) {
    // Khóa / mở khóa tài khoản.
    var lock = e.target.closest("[data-lock]");
    if (lock) {
      e.preventDefault();
      var id = parseInt(lock.getAttribute("data-lock"), 10);
      try {
        var r = await Forum.api.post("/quan-tri/nguoi-dung/khoa", { id: id });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        var row = lock.closest("[data-user-row]");
        lock.classList.toggle("btn-danger", !r.locked);
        var lbl = lock.querySelector("[data-lock-label]");
        if (lbl) lbl.textContent = r.locked ? "Mở khóa" : "Khóa";
        if (row) {
          var lb = row.querySelector("[data-lock-badge]"); if (lb) lb.style.display = r.locked ? "" : "none";
          var ab = row.querySelector("[data-active-badge]"); if (ab) ab.style.display = r.locked ? "none" : "";
        }
        Forum.toast(r.locked ? "Đã khóa tài khoản." : "Đã mở khóa.", "success", 1600);
      } catch (ex) { err(ex); }
      return;
    }

    // Người dùng: khóa có thời hạn.
    var bn = e.target.closest("[data-ban]");
    if (bn) {
      e.preventDefault();
      var daysEl = document.getElementById("ban-days");
      var reasonEl = document.getElementById("ban-reason");
      var days = parseInt((daysEl && daysEl.value) || "0", 10);
      if (!confirm(days > 0 ? ("Khóa tài khoản " + days + " ngày?") : "Khóa vĩnh viễn tài khoản này?")) return;
      try {
        var r = await Forum.api.post("/quan-tri/nguoi-dung/cam", { id: parseInt(bn.getAttribute("data-ban"), 10), days: days, reason: (reasonEl && reasonEl.value) || "" });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        Forum.toast("Đã khóa tài khoản.", "success"); location.reload();
      } catch (ex) { err(ex); }
      return;
    }
    // Người dùng: mở khóa (trang chi tiết).
    var uu = e.target.closest("[data-user-unlock]");
    if (uu) {
      e.preventDefault();
      try { await Forum.api.post("/quan-tri/nguoi-dung/khoa", { id: parseInt(uu.getAttribute("data-user-unlock"), 10) }); Forum.toast("Đã mở khóa.", "success"); location.reload(); }
      catch (ex) { err(ex); }
      return;
    }
    // Người dùng: gửi cảnh cáo.
    var wn = e.target.closest("[data-warn]");
    if (wn) {
      e.preventDefault();
      var reason = prompt("Lý do cảnh cáo:");
      if (reason == null || !reason.trim()) return;
      try {
        var r = await Forum.api.post("/quan-tri/nguoi-dung/canh-cao", { id: parseInt(wn.getAttribute("data-warn"), 10), reason: reason.trim() });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        Forum.toast("Đã gửi cảnh cáo.", "success"); location.reload();
      } catch (ex) { err(ex); }
      return;
    }
    // Bình luận: khôi phục.
    var cr = e.target.closest("[data-comment-restore]");
    if (cr) {
      e.preventDefault();
      try {
        var r = await Forum.api.post("/quan-tri/noi-dung/binh-luan/khoi-phuc", { id: parseInt(cr.getAttribute("data-comment-restore"), 10) });
        if (!r.ok) { Forum.toast("Không khôi phục được.", "error"); return; }
        Forum.toast("Đã khôi phục bình luận.", "success", 1500); location.reload();
      } catch (ex) { err(ex); }
      return;
    }

    // Sắp xếp danh mục (lên/xuống).
    var mv = e.target.closest("[data-move]");
    if (mv) {
      e.preventDefault();
      var row = mv.closest("[data-cat-row]");
      var tbody = row && row.parentElement;
      if (!row || !tbody) return;
      if (mv.getAttribute("data-move") === "up" && row.previousElementSibling)
        tbody.insertBefore(row, row.previousElementSibling);
      else if (mv.getAttribute("data-move") === "down" && row.nextElementSibling)
        tbody.insertBefore(row.nextElementSibling, row);
      else return;
      var ids = Array.from(tbody.querySelectorAll("[data-cat-row]")).map(function (r) { return parseInt(r.getAttribute("data-cat-row"), 10); });
      try { await Forum.api.post("/quan-tri/danh-muc/thu-tu", ids); Forum.toast("Đã đổi thứ tự.", "success", 1200); }
      catch (ex) { err(ex); }
      return;
    }

    // Sửa danh mục → đổ dữ liệu vào form.
    var ec = e.target.closest("[data-edit-cat]");
    if (ec) {
      e.preventDefault();
      var g = function (k) { return ec.getAttribute("data-" + k) || ""; };
      document.getElementById("cat-id").value = g("id");
      document.getElementById("cat-name").value = g("name");
      document.getElementById("cat-desc").value = g("desc");
      document.getElementById("cat-icon").value = g("icon") || "door-open";
      document.getElementById("cat-color").value = g("color") || "#4f8cff";
      document.getElementById("cat-order").value = g("order") || "0";
      document.getElementById("cat-approval").checked = g("approval") === "true";
      var mr = document.getElementById("cat-minrole"); if (mr) mr.value = g("minrole");
      document.getElementById("cat-mods").value = g("mods");
      document.getElementById("cat-form-title").textContent = "Sửa danh mục: " + g("name");
      document.getElementById("cat-form").scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    var reset = e.target.closest("#cat-reset");
    if (reset) {
      e.preventDefault();
      document.getElementById("cat-form").reset();
      document.getElementById("cat-id").value = "0";
      document.getElementById("cat-form-title").textContent = "Thêm danh mục";
      return;
    }

    // Thẻ: đổi tên.
    var rn = e.target.closest("[data-tag-rename]");
    if (rn) {
      e.preventDefault();
      var id = parseInt(rn.getAttribute("data-tag-rename"), 10);
      var name = prompt("Tên mới cho thẻ:", rn.getAttribute("data-name"));
      if (name == null || !name.trim()) return;
      try {
        var r = await Forum.api.post("/quan-tri/the/sua", { id: id, name: name.trim() });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        var row = rn.closest("[data-tag-row]");
        if (row) { row.querySelector("[data-tag-name]").textContent = "#" + r.name; row.querySelector("[data-tag-slug]").textContent = r.slug; }
        rn.setAttribute("data-name", r.name);
        Forum.toast("Đã đổi tên thẻ.", "success", 1500);
      } catch (ex) { err(ex); }
      return;
    }

    // Thẻ: gộp vào thẻ khác.
    var mg = e.target.closest("[data-tag-merge]");
    if (mg) {
      e.preventDefault();
      var id = parseInt(mg.getAttribute("data-tag-merge"), 10);
      var into = prompt("Gộp thẻ #" + mg.getAttribute("data-name") + " vào thẻ nào? Nhập TÊN thẻ đích:");
      if (into == null || !into.trim()) return;
      try {
        var r = await Forum.api.post("/quan-tri/the/gop", { fromId: id, intoName: into.trim() });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        var row = mg.closest("[data-tag-row]"); if (row) row.remove();
        Forum.toast("Đã gộp vào #" + r.into + " (" + r.useCount + " lượt dùng).", "success", 2200);
      } catch (ex) { err(ex); }
      return;
    }

    // Thẻ: xóa.
    var dl = e.target.closest("[data-tag-del]");
    if (dl) {
      e.preventDefault();
      var id = parseInt(dl.getAttribute("data-tag-del"), 10);
      if (!confirm("Xóa thẻ #" + dl.getAttribute("data-name") + "? Sẽ gỡ thẻ khỏi mọi chủ đề.")) return;
      try {
        var r = await Forum.api.post("/quan-tri/the/xoa", { id: id });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        var row = dl.closest("[data-tag-row]"); if (row) row.remove();
        Forum.toast("Đã xóa thẻ.", "success", 1500);
      } catch (ex) { err(ex); }
      return;
    }

    // Nội dung: thao tác chủ đề (ghim/khóa/nổi bật/xóa/khôi phục) → nạp lại trang.
    var ta = e.target.closest("[data-topic-act]");
    if (ta) {
      e.preventDefault();
      var conf = ta.getAttribute("data-confirm");
      if (conf && !confirm(conf)) return;
      try {
        var r = await Forum.api.post("/quan-tri/noi-dung/chu-de", { id: parseInt(ta.getAttribute("data-id"), 10), action: ta.getAttribute("data-act") });
        if (!r.ok) { Forum.toast("Không thực hiện được.", "error"); return; }
        location.reload();
      } catch (ex) { err(ex); }
      return;
    }

    // Nội dung: xóa hàng loạt chủ đề đã chọn.
    var bulk = e.target.closest("#bulk-del-topics");
    if (bulk) {
      e.preventDefault();
      var ids = Array.from(document.querySelectorAll(".topic-chk:checked")).map(function (c) { return parseInt(c.value, 10); });
      if (!ids.length) { Forum.toast("Chưa chọn mục nào.", "warning"); return; }
      if (!confirm("Xóa " + ids.length + " chủ đề đã chọn?")) return;
      try { var r = await Forum.api.post("/quan-tri/noi-dung/xoa-nhieu", ids); Forum.toast("Đã xóa " + r.deleted + " chủ đề.", "success"); location.reload(); }
      catch (ex) { err(ex); }
      return;
    }

    // Nội dung: xóa bình luận.
    var cd = e.target.closest("[data-comment-del]");
    if (cd) {
      e.preventDefault();
      if (!confirm("Xóa bình luận này?")) return;
      try {
        var r = await Forum.api.post("/quan-tri/noi-dung/binh-luan/xoa", { id: parseInt(cd.getAttribute("data-comment-del"), 10) });
        if (!r.ok) { Forum.toast("Không xóa được.", "error"); return; }
        var row = cd.closest("[data-comment-row]"); if (row) row.remove();
        Forum.toast("Đã xóa bình luận.", "success", 1500);
      } catch (ex) { err(ex); }
      return;
    }

    // Huy hiệu: trao / thu hồi.
    function badgeUpdate(btn, r) {
      var row = btn.closest("[data-badge-row]");
      var h = row && row.querySelector("[data-badge-holders]");
      if (h) h.textContent = r.holders;
    }
    var ba = e.target.closest("[data-badge-award]");
    if (ba) {
      e.preventDefault();
      var uname = prompt("Trao huy hiệu cho ai? Nhập tên đăng nhập:");
      if (uname == null || !uname.trim()) return;
      try {
        var r = await Forum.api.post("/quan-tri/huy-hieu/trao", { badgeId: parseInt(ba.getAttribute("data-badge-award"), 10), userName: uname.trim() });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        badgeUpdate(ba, r); Forum.toast("Đã trao huy hiệu.", "success", 1600);
      } catch (ex) { err(ex); }
      return;
    }
    var brv = e.target.closest("[data-badge-revoke]");
    if (brv) {
      e.preventDefault();
      var uname = prompt("Thu hồi huy hiệu của ai? Nhập tên đăng nhập:");
      if (uname == null || !uname.trim()) return;
      try {
        var r = await Forum.api.post("/quan-tri/huy-hieu/thu-hoi", { badgeId: parseInt(brv.getAttribute("data-badge-revoke"), 10), userName: uname.trim() });
        if (!r.ok) { Forum.toast(r.error, "error"); return; }
        badgeUpdate(brv, r); Forum.toast("Đã thu hồi huy hiệu.", "success", 1600);
      } catch (ex) { err(ex); }
      return;
    }

    // Huy hiệu: sửa (đổ vào form) / làm mới.
    var eb = e.target.closest("[data-edit-badge]");
    if (eb) {
      e.preventDefault();
      var g = function (k) { return eb.getAttribute("data-" + k) || ""; };
      document.getElementById("badge-id").value = g("id");
      document.getElementById("badge-name").value = g("name");
      document.getElementById("badge-desc").value = g("desc");
      document.getElementById("badge-icon").value = g("icon") || "award";
      document.getElementById("badge-color").value = g("color") || "#c9a227";
      document.getElementById("badge-tier").value = g("tier") || "Bronze";
      document.getElementById("badge-form-title").textContent = "Sửa huy hiệu: " + g("name");
      document.getElementById("badge-form").scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    var breset = e.target.closest("#badge-reset");
    if (breset) {
      e.preventDefault();
      document.getElementById("badge-form").reset();
      document.getElementById("badge-id").value = "0";
      document.getElementById("badge-form-title").textContent = "Thêm huy hiệu";
      return;
    }

    // Thông báo chạy: sửa / làm mới form.
    var ea = e.target.closest("[data-edit-ann]");
    if (ea) {
      e.preventDefault();
      var g = function (k) { return ea.getAttribute("data-" + k) || ""; };
      document.getElementById("ann-id").value = g("id");
      document.getElementById("ann-msg").value = g("msg");
      document.getElementById("ann-url").value = g("url");
      document.getElementById("ann-order").value = g("order") || "0";
      document.getElementById("ann-start").value = g("start");
      document.getElementById("ann-end").value = g("end");
      document.getElementById("ann-active").checked = g("active") === "true";
      document.getElementById("ann-form-title").textContent = "Sửa thông báo";
      document.getElementById("ann-form").scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    var areset = e.target.closest("#ann-reset");
    if (areset) {
      e.preventDefault();
      document.getElementById("ann-form").reset();
      document.getElementById("ann-id").value = "0";
      document.getElementById("ann-form-title").textContent = "Thêm thông báo";
      return;
    }
  });
})();
