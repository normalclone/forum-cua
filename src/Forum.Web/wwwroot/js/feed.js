/* =========================================================================
   Feed AJAX (jQuery): đổi tab sắp xếp (Hoạt động / Mới / Nổi bật / Xu hướng)
   và phân trang KHÔNG reload trang — tải phần danh sách rồi thay vào #feed.
   Tiến bộ hóa: các link vẫn điều hướng bình thường nếu JS lỗi/tắt.
   ========================================================================= */
(function ($) {
  "use strict";
  if (!$) return;
  var Forum = window.Forum || {};
  var $feed = $("#feed");
  if (!$feed.length) return;

  // Lấy giá trị ?sap-xep= của một URL ("" nếu không có = "Hoạt động").
  function sortKeyOf(url) {
    try { return new URL(url, location.origin).searchParams.get("sap-xep") || ""; }
    catch (e) { return ""; }
  }

  // Đồng bộ tab "active" theo URL hiện tại.
  function syncTabs(url) {
    var target = sortKeyOf(url);
    $(".feed-toolbar .seg a").each(function () {
      $(this).toggleClass("active", sortKeyOf(this.href) === target);
    });
  }

  function load(url, push) {
    if (Forum.progress) Forum.progress.start();
    $feed.addClass("feed-loading");
    $.ajax({ url: url, cache: false, headers: { "X-Requested-With": "XMLHttpRequest" } })
      .done(function (html) {
        $feed.html(html);
        if (push) history.pushState({ feed: url }, "", url);
        syncTabs(url);
        if (Forum.observeReveal) Forum.observeReveal($feed[0]); // hiện animation cho card mới
      })
      .fail(function () { window.location.href = url; }) // dự phòng: điều hướng thật
      .always(function () {
        $feed.removeClass("feed-loading");
        if (Forum.progress) Forum.progress.done();
      });
  }

  function go(url) {
    if (!url) return;
    load(url, true);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  // Tab sắp xếp (nằm ngoài #feed) — uỷ quyền trên document.
  $(document).on("click", ".feed-toolbar .seg a", function (e) {
    e.preventDefault();
    go(this.getAttribute("href"));
  });

  // Phân trang (nằm trong #feed, thay mới sau mỗi lần load) — uỷ quyền trên #feed.
  $feed.on("click", ".pager a", function (e) {
    e.preventDefault();
    go(this.getAttribute("href"));
  });

  // Back/Forward: nạp lại feed theo URL trong lịch sử mà không reload trang.
  window.addEventListener("popstate", function () {
    load(location.pathname + location.search, false);
  });
})(window.jQuery);
