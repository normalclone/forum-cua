/* Bảng kiểm duyệt: giải quyết/bỏ qua/xóa báo cáo. */
(function () {
  "use strict";
  const Forum = window.Forum || {};

  function removeRow(id) {
    const row = document.querySelector(`[data-report-row="${id}"]`);
    if (row) { row.classList.add("collapsing-out"); setTimeout(() => row.remove(), 300); }
  }

  document.addEventListener("click", async e => {
    const resolve = e.target.closest("[data-resolve]");
    const dismiss = e.target.closest("[data-dismiss]");
    const del = e.target.closest("[data-mod-delete]");

    if (resolve || dismiss) {
      e.preventDefault();
      const id = (resolve || dismiss).dataset.resolve || (resolve || dismiss).dataset.dismiss;
      try {
        await Forum.api.post("/kiem-duyet/bao-cao/giai-quyet", { id: parseInt(id, 10), dismiss: !!dismiss });
        removeRow(id);
        Forum.toast(dismiss ? "Đã bỏ qua báo cáo." : "Đã giải quyết báo cáo.", "success", 1500);
      } catch (err) { Forum.toast(err.message || "Lỗi.", "error"); }
      return;
    }

    if (del) {
      e.preventDefault();
      if (!await Forum.confirm({ title: "Xóa nội dung", message: "Xóa nội dung bị báo cáo này?" })) return;
      try {
        await Forum.api.post("/kiem-duyet/xoa", { type: del.dataset.type, id: parseInt(del.dataset.target, 10) });
        removeRow(del.dataset.modDelete);
        Forum.toast("Đã xóa nội dung.", "success", 1500);
      } catch (err) { Forum.toast(err.message || "Lỗi.", "error"); }
      return;
    }

    // Duyệt / từ chối bài chờ kiểm duyệt.
    const approve = e.target.closest("[data-approve]");
    const reject = e.target.closest("[data-reject]");
    if (approve || reject) {
      e.preventDefault();
      const id = parseInt((approve || reject).dataset.approve || (approve || reject).dataset.reject, 10);
      let reason = null;
      if (reject) {
        reason = prompt("Lý do từ chối (tuỳ chọn):", "");
        if (reason === null) return; // huỷ
      }
      try {
        const url = approve ? "/kiem-duyet/duyet" : "/kiem-duyet/tu-choi";
        const r = await Forum.api.post(url, { id: id, reason: reason });
        if (!r.ok) { Forum.toast("Không thực hiện được.", "error"); return; }
        const row = document.querySelector(`[data-approval-row="${id}"]`);
        if (row) { row.classList.add("collapsing-out"); setTimeout(() => row.remove(), 300); }
        Forum.toast(approve ? "Đã duyệt & hiển thị bài." : "Đã từ chối bài.", "success", 1600);
      } catch (err) { Forum.toast(err.message || "Lỗi.", "error"); }
    }
  });
})();
