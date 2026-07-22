/* =========================================================================
   Đăng nhập dạng popup: nạp form /dang-nhap vào modal, submit bằng jQuery/AJAX.
   Tiến bộ hóa: link "Đăng nhập" vẫn mở trang đầy đủ nếu JS lỗi/tắt (giữ href).
   ========================================================================= */
(function () {
  "use strict";
  var Forum = window.Forum;
  if (!Forum || !Forum.modal) return;

  async function openLogin(returnUrl) {
    var url = "/dang-nhap?returnUrl=" + encodeURIComponent(returnUrl || (location.pathname + location.search));
    var html;
    try {
      var res = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
      html = await res.text();
    } catch (e) { window.location = "/dang-nhap"; return; }

    var closeBtn = '<button type="button" class="modal-close" aria-label="Đóng" title="Đóng">' +
      '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>' +
      '</button>';
    var m = Forum.modal(closeBtn + '<div class="auth-modal"></div>');
    m.el.classList.add("modal-auth");
    m.el.querySelector(".auth-modal").innerHTML = html;
    // Nút X nằm ngoài .auth-modal nên vẫn còn khi form được render lại (lỗi xác thực).
    m.el.querySelector(".modal-close").addEventListener("click", function () { m.close(); });
    wire(m);
  }

  function wire(m) {
    var form = m.el.querySelector("form[data-login-form]");
    if (!form) return;
    var first = form.querySelector('input[name="UserNameOrEmail"]');
    if (first) setTimeout(function () { first.focus(); }, 50);

    form.addEventListener("submit", async function (e) {
      e.preventDefault();
      var btn = form.querySelector('button[type="submit"]');
      if (btn) btn.disabled = true;
      if (Forum.progress) Forum.progress.start();
      try {
        var res = await fetch("/dang-nhap", {
          method: "POST",
          headers: { "X-Requested-With": "XMLHttpRequest" },
          body: new FormData(form)
        });
        var ct = res.headers.get("content-type") || "";
        if (ct.indexOf("application/json") !== -1) {
          var data = await res.json();
          if (data && data.ok) { window.location = data.redirect || "/"; return; }
        }
        // Lỗi xác thực: server trả lại form (kèm thông báo) → thay nội dung & gắn lại sự kiện.
        var errHtml = await res.text();
        m.el.querySelector(".auth-modal").innerHTML = errHtml;
        wire(m);
      } catch (err) {
        Forum.toast("Không đăng nhập được, vui lòng thử lại.", "error");
        if (btn) btn.disabled = false;
      } finally {
        if (Forum.progress) Forum.progress.done();
      }
    });
  }

  // Bất kỳ phần tử [data-login] nào (vd nút "Đăng nhập" ở header) → mở popup.
  document.addEventListener("click", function (e) {
    var trigger = e.target.closest("[data-login]");
    if (!trigger) return;
    e.preventDefault();
    openLogin(); // returnUrl mặc định = trang hiện tại
  });
})();
