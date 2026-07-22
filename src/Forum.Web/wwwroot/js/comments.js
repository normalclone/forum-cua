/* =========================================================================
   Bình luận: trả lời lồng + @mention, gửi AJAX, sửa/xóa tại chỗ, thu gọn nhánh.
   ========================================================================= */
(function () {
  "use strict";
  const Forum = window.Forum || {};
  const $ = (s, c) => (c || document).querySelector(s);

  function makeForm(topicId, parentId, prefill) {
    const f = document.createElement("form");
    f.className = "card card-pad comment-form-wrap reply-form";
    f.setAttribute("data-comment-form", "");
    f.dataset.topic = topicId;
    if (parentId) f.dataset.parent = parentId;
    f.innerHTML = `<textarea data-md-editor name="body" rows="2" placeholder="Viết trả lời…">${prefill || ""}</textarea>
      <div class="flex between items-center mt-8">
        <span class="hint">Hỗ trợ Markdown</span>
        <span><button type="button" class="btn btn-sm reply-cancel">Hủy</button>
        <button type="submit" class="btn btn-primary btn-sm">Gửi</button></span>
      </div>`;
    return f;
  }

  function updateCounts(delta) {
    document.querySelectorAll("[data-comment-count], #binh-luan h2").forEach(() => {});
    const h2 = $("#binh-luan h2");
    if (h2) { const n = (parseInt(h2.textContent) || 0) + delta; h2.textContent = `${n} bình luận`; }
  }

  // ---- Reply: mở form lồng + chèn @mention ----
  document.addEventListener("click", e => {
    const reply = e.target.closest("[data-reply]");
    if (reply) {
      e.preventDefault();
      if (!Forum.isAuthenticated) { Forum.toast("Đăng nhập để trả lời.", "warning"); return; }
      const node = reply.closest(".comment-node");
      const existing = node.querySelector(":scope > .reply-form");
      if (existing) { existing.remove(); return; }
      const topicId = $("[data-comment-form]").dataset.topic;
      const author = reply.dataset.author;
      const form = makeForm(topicId, reply.dataset.comment || node.dataset.commentId, author ? `@${author} ` : "");
      const commentEl = node.querySelector(":scope > .comment");
      commentEl.after(form);
      Forum.enhanceEditors(form);
      const ta = form.querySelector("textarea");
      ta.focus(); ta.setSelectionRange(ta.value.length, ta.value.length);
      return;
    }
    const cancel = e.target.closest(".reply-cancel");
    if (cancel) { e.preventDefault(); cancel.closest(".reply-form")?.remove(); return; }

    // collapse
    const col = e.target.closest(".comment-collapse");
    if (col) { e.preventDefault(); col.closest(".comment-node").classList.toggle("collapsed"); return; }

    // edit
    const edit = e.target.closest("[data-edit-comment]");
    if (edit) { e.preventDefault(); openEdit(edit.closest(".comment-node"), edit.dataset.editComment); return; }

    // delete
    const del = e.target.closest("[data-delete-comment]");
    if (del) { e.preventDefault(); doDelete(del.dataset.deleteComment, del.closest(".comment-node")); return; }
  });

  // ---- Submit (composer chính + reply form) ----
  document.addEventListener("submit", async e => {
    const form = e.target.closest("[data-comment-form]");
    if (!form) return;
    e.preventDefault();
    const ta = form.querySelector("textarea[name=body]");
    const body = ta.value.trim();
    if (body.length < 2) { Forum.toast("Bình luận quá ngắn.", "warning"); return; }
    const btn = form.querySelector("button[type=submit]");
    btn.disabled = true;
    try {
      const r = await Forum.api.post("/binh-luan/them", {
        topicId: parseInt(form.dataset.topic, 10),
        parentId: form.dataset.parent ? parseInt(form.dataset.parent, 10) : null,
        body
      });
      const tmp = document.createElement("div");
      tmp.innerHTML = r.html.trim();
      const node = tmp.firstElementChild;

      if (form.dataset.parent) {
        const parentNode = document.getElementById("comment-" + form.dataset.parent);
        let kids = parentNode.querySelector(":scope > .comment-children");
        kids.appendChild(node);
        form.remove();
      } else {
        $("#no-comments")?.remove();
        const tree = $("#comment-tree");
        tree.appendChild(node);
        ta.value = "";
        const preview = form.querySelector(".editor-preview"); if (preview) preview.innerHTML = "";
      }
      node.querySelector(".comment")?.classList.add("highlight");
      Forum.enhanceEditors(node);
      updateCounts(1);
      node.scrollIntoView({ behavior: "smooth", block: "center" });
      Forum.toast("Đã đăng bình luận.", "success", 1600);
    } catch (err) {
      Forum.toast(err.message || "Không gửi được.", "error");
    } finally { btn.disabled = false; }
  });

  function openEdit(node, id) {
    const body = node.querySelector(":scope > .comment > .comment-body");
    if (!body || node.querySelector(":scope > .edit-form")) return;
    const original = body.innerText;
    const form = document.createElement("form");
    form.className = "edit-form mt-8";
    form.innerHTML = `<textarea data-md-editor name="body" rows="3">${original}</textarea>
      <div class="flex gap-6 mt-8"><button type="submit" class="btn btn-primary btn-sm">Lưu</button>
      <button type="button" class="btn btn-sm edit-cancel">Hủy</button></div>`;
    body.after(form);
    body.classList.add("hide");
    Forum.enhanceEditors(form);
    form.querySelector(".edit-cancel").addEventListener("click", () => { form.remove(); body.classList.remove("hide"); });
    form.addEventListener("submit", async ev => {
      ev.preventDefault();
      const v = form.querySelector("textarea[name=body]").value.trim();
      try {
        const r = await Forum.api.post(`/binh-luan/${id}/sua`, { body: v });
        body.innerHTML = r.html;
        body.classList.remove("hide");
        form.remove();
        const head = node.querySelector(":scope > .comment > .comment-head");
        if (head && !head.querySelector(".edited-tag")) {
          const s = document.createElement("span"); s.className = "edited-tag"; s.textContent = "· đã chỉnh sửa"; head.appendChild(s);
        }
        Forum.toast("Đã cập nhật.", "success", 1500);
      } catch (err) { Forum.toast(err.message || "Lỗi.", "error"); }
    });
  }

  async function doDelete(id, node) {
    if (!await Forum.confirm({ title: "Xóa bình luận", message: "Bạn chắc chắn muốn xóa bình luận này?" })) return;
    try {
      const r = await Forum.api.post(`/binh-luan/${id}/xoa`, {});
      if (r.ok) {
        const commentEl = node.querySelector(":scope > .comment");
        commentEl.classList.add("deleted");
        const body = commentEl.querySelector(".comment-body"); if (body) body.outerHTML = `<div class="comment-body muted" style="font-style:italic">[Bình luận đã bị xóa]</div>`;
        commentEl.querySelector(".comment-actions")?.remove();
        const head = commentEl.querySelector(".comment-head");
        updateCounts(-1);
        Forum.toast("Đã xóa bình luận.", "success", 1500);
      }
    } catch (err) { Forum.toast(err.message || "Không xóa được.", "error"); }
  }
})();
