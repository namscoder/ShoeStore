// Thư viện ảnh trong form thêm/sửa sản phẩm:
// - Nhóm "Ảnh chung" luôn có; mỗi màu đang chọn trong bảng size/màu có 1 nhóm ảnh riêng (tự thêm/bớt theo bảng).
// - Ảnh mới: mỗi lần chọn được CỘNG DỒN vào nhóm; bấm × để bỏ.
// - Ảnh cũ (trang Sửa): bấm × để đánh dấu xoá, bấm lại để hoàn tác. Chỉ xoá thật khi bấm Lưu.
(function () {
    const container = document.getElementById("imageGroups");
    const template = document.getElementById("imageGroupTemplate");
    const variantRows = document.getElementById("variantRows");

    if (!container || !template || !variantRows) {
        return;
    }

    const allowed = container.dataset.allowed.split(",");
    const maxSize = Number(container.dataset.maxSize);
    const maxFiles = Number(container.dataset.maxFiles);
    const maxTotal = Number(container.dataset.maxTotal);

    // Danh sách file mới đã chọn của từng ô input
    const filesOf = new WeakMap();

    function getFiles(input) {
        return filesOf.get(input) || [];
    }

    // Ghi danh sách file vào ô input (để form gửi lên server) và nhớ lại danh sách
    function setFiles(input, files) {
        const transfer = new DataTransfer();
        files.forEach(function (file) {
            transfer.items.add(file);
        });
        input.files = transfer.files;
        filesOf.set(input, files);
    }

    function totalSize() {
        let total = 0;
        container.querySelectorAll('input[type="file"]').forEach(function (input) {
            getFiles(input).forEach(function (file) {
                total += file.size;
            });
        });
        return total;
    }

    function checkFile(file) {
        const extension = file.name.split(".").pop().toLowerCase();
        if (!allowed.includes(extension)) {
            return '"' + file.name + '": chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp';
        }
        if (file.size > maxSize) {
            return '"' + file.name + '": ảnh không được vượt quá ' + (maxSize / 1024 / 1024) + "MB";
        }
        return null;
    }

    // Số ảnh cũ còn giữ lại (chưa bị đánh dấu xoá) của 1 nhóm
    function keptExistingCount(group) {
        return group.querySelectorAll("[data-existing-image]:not(.marked-delete)").length;
    }

    // Vẽ lại ảnh mới xem trước + số ảnh của 1 nhóm
    function render(group) {
        const input = group.querySelector('input[type="file"]');
        const files = getFiles(input);
        const previews = group.querySelector('[data-role="previews"]');
        const existingTotal = group.querySelectorAll("[data-existing-image]").length;

        previews.querySelectorAll("img").forEach(function (img) {
            URL.revokeObjectURL(img.src); // giải phóng bộ nhớ của ảnh xem trước cũ
        });
        previews.innerHTML = "";

        if (files.length === 0 && existingTotal === 0) {
            previews.innerHTML = '<span class="text-muted small">Chưa có ảnh</span>';
        }

        files.forEach(function (file, index) {
            const item = document.createElement("div");
            item.className = "position-relative";
            item.innerHTML =
                '<img class="rounded border border-success" style="width: 72px; height: 72px; object-fit: cover;" />' +
                '<span class="badge bg-success position-absolute bottom-0 start-0 m-1">Mới</span>' +
                '<button type="button" class="btn btn-danger btn-sm position-absolute top-0 end-0 py-0 px-1 lh-1"' +
                ' data-remove-index="' + index + '" aria-label="Bỏ ảnh này">&times;</button>';

            const img = item.querySelector("img");
            img.src = URL.createObjectURL(file);
            img.alt = file.name;
            img.title = file.name;

            previews.appendChild(item);
        });

        const count = keptExistingCount(group) + files.length;
        group.querySelector('[data-role="count"]').textContent = count > 0 ? count + "/" + maxFiles + " ảnh" : "";
    }

    // Màu đang được chọn trong bảng size/màu: Map(id => tên màu)
    function selectedColors() {
        const colors = new Map();
        variantRows.querySelectorAll('select[name$=".ColorId"]').forEach(function (select) {
            if (select.value) {
                colors.set(select.value, select.selectedOptions[0].text);
            }
        });
        return colors;
    }

    // Thêm/bớt nhóm ảnh theo các màu trong bảng size/màu, rồi đánh số lại cho server
    function syncGroups() {
        const colors = selectedColors();

        container.querySelectorAll(".image-group").forEach(function (group) {
            const id = group.dataset.colorId;
            if (id === "") {
                return; // nhóm "Ảnh chung" luôn giữ
            }

            const inTable = colors.has(id);
            const hasExisting = group.querySelectorAll("[data-existing-image]").length > 0;

            // Màu không còn trong bảng và không có ảnh cũ => bỏ nhóm
            if (!inTable && !hasExisting) {
                group.remove();
                return;
            }

            // Màu không còn trong bảng nhưng còn ảnh cũ => giữ nhóm để người dùng xoá ảnh, không cho thêm ảnh mới
            const input = group.querySelector('input[type="file"]');
            group.querySelector('[data-role="add-button"]').classList.toggle("d-none", !inTable);
            if (!inTable && getFiles(input).length > 0) {
                setFiles(input, []);
                render(group);
            }

            group.querySelector('[data-role="warning"]').textContent =
                inTable || keptExistingCount(group) === 0
                    ? ""
                    : "Màu này đã bị bỏ khỏi bảng size/màu nhưng vẫn còn ảnh. Hãy xoá hết ảnh của màu này (bấm ×) hoặc thêm lại màu vào bảng.";
        });

        colors.forEach(function (name, id) {
            let group = container.querySelector('.image-group[data-color-id="' + id + '"]');
            if (!group) {
                group = template.content.querySelector(".image-group").cloneNode(true);
                group.dataset.colorId = id;
                group.querySelector('[data-role="color-id"]').value = id;
                container.appendChild(group);
                render(group);
            }
            group.querySelector('[data-role="title"]').textContent = "Màu " + name;
        });

        // Chưa chọn màu nào => hiện dòng hướng dẫn cách thêm ảnh theo màu
        const hint = document.getElementById("colorGroupsHint");
        if (hint) {
            hint.hidden = colors.size > 0;
        }

        // Đánh số [0], [1], [2]... để server nhận đúng danh sách ImageGroups
        container.querySelectorAll(".image-group").forEach(function (group, index) {
            group.querySelector('[data-role="color-id"]').name = "ImageGroups[" + index + "].ColorId";
            group.querySelector('input[type="file"]').name = "ImageGroups[" + index + "].Files";
        });
    }

    // Chọn ảnh mới: kiểm tra từng file, file hợp lệ được cộng dồn vào nhóm
    container.addEventListener("change", function (event) {
        const input = event.target;
        if (!input.matches('input[type="file"]')) {
            return;
        }

        const group = input.closest(".image-group");
        const current = getFiles(input);
        const picked = Array.from(input.files);
        const accepted = [];
        const errors = [];
        const kept = keptExistingCount(group);
        let total = totalSize();

        picked.forEach(function (file) {
            const error = checkFile(file);
            if (error) {
                errors.push(error);
            } else if (kept + current.length + accepted.length >= maxFiles) {
                errors.push("Mỗi nhóm tối đa " + maxFiles + " ảnh (tính cả ảnh cũ)");
            } else if (total + file.size > maxTotal) {
                errors.push("Tổng dung lượng thư viện ảnh quá lớn");
            } else {
                accepted.push(file);
                total += file.size;
            }
        });

        setFiles(input, current.concat(accepted));
        group.querySelector('[data-role="error"]').textContent = Array.from(new Set(errors)).join(" · ");
        render(group);
    });

    container.addEventListener("click", function (event) {
        // Ảnh cũ: bấm × để đánh dấu xoá, bấm ↺ để hoàn tác
        const deleteButton = event.target.closest("[data-delete-image-id]");
        if (deleteButton) {
            const item = deleteButton.closest("[data-existing-image]");
            const marked = item.classList.toggle("marked-delete");

            item.querySelector("img").classList.toggle("opacity-25", marked);
            item.querySelector('input[name="DeleteImageIds"]').disabled = !marked; // chỉ gửi lên server khi bị đánh dấu
            deleteButton.innerHTML = marked ? "&#8634;" : "&times;";
            deleteButton.classList.toggle("btn-danger", !marked);
            deleteButton.classList.toggle("btn-secondary", marked);
            deleteButton.setAttribute("aria-label", marked ? "Hoàn tác xoá ảnh" : "Xoá ảnh này");

            render(item.closest(".image-group"));
            syncGroups();
            return;
        }

        // Ảnh mới: bấm × để bỏ khỏi nhóm
        const removeButton = event.target.closest("[data-remove-index]");
        if (removeButton) {
            const group = removeButton.closest(".image-group");
            const input = group.querySelector('input[type="file"]');
            const files = getFiles(input).slice();

            files.splice(Number(removeButton.dataset.removeIndex), 1);
            setFiles(input, files);
            group.querySelector('[data-role="error"]').textContent = "";
            render(group);
        }
    });

    // Đổi màu ở 1 dòng, hoặc thêm/xoá dòng trong bảng size/màu => cập nhật nhóm ảnh
    variantRows.addEventListener("change", function (event) {
        if (event.target.matches('select[name$=".ColorId"]')) {
            syncGroups();
        }
    });
    new MutationObserver(syncGroups).observe(variantRows, { childList: true });

    container.querySelectorAll(".image-group").forEach(render);
    syncGroups();
})();
