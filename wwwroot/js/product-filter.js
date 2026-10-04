// Khung bộ lọc trang danh sách sản phẩm (admin):
// chọn "Thêm điều kiện lọc" -> chỉ hiện ô lọc đó; bấm dấu x -> xoá giá trị và ẩn ô lọc.
(function () {
    const form = document.getElementById("productFilterForm");
    if (!form) {
        return;
    }

    const emptyHint = document.getElementById("filterEmptyHint");
    const addDropdown = document.getElementById("addFilterDropdown");

    function fieldOf(key) {
        return form.querySelector('.filter-field[data-filter="' + key + '"]');
    }

    function menuItemOf(key) {
        return form.querySelector('[data-add-filter="' + key + '"]');
    }

    // Cập nhật dòng gợi ý và nút "Thêm điều kiện lọc"
    function refresh() {
        const visibleFields = form.querySelectorAll(".filter-field:not([hidden])").length;
        const remainingItems = form.querySelectorAll("[data-add-filter]:not([hidden])").length;

        emptyHint.hidden = visibleFields > 0;   // đã có ô lọc thì ẩn dòng gợi ý
        addDropdown.hidden = remainingItems === 0; // đã thêm hết điều kiện thì ẩn nút
    }

    form.addEventListener("click", function (event) {
        // Chọn 1 điều kiện trong menu "Thêm điều kiện lọc"
        const addItem = event.target.closest("[data-add-filter]");
        if (addItem) {
            const key = addItem.dataset.addFilter;
            const field = fieldOf(key);

            field.hidden = false;
            addItem.hidden = true;

            const firstInput = field.querySelector("input, select");
            if (firstInput) {
                firstInput.focus();
            }

            refresh();
            return;
        }

        // Bấm dấu x trên 1 ô lọc
        const removeButton = event.target.closest("[data-remove-filter]");
        if (removeButton) {
            const key = removeButton.dataset.removeFilter;
            const field = fieldOf(key);

            field.querySelectorAll("input, select").forEach(function (el) {
                el.value = "";
            });
            field.hidden = true;
            menuItemOf(key).hidden = false;

            refresh();
        }
    });

    // Khi gửi form: không gửi các ô đang ẩn hoặc để trống => URL gọn, chỉ chứa điều kiện thật sự dùng
    form.addEventListener("submit", function () {
        form.querySelectorAll("input, select").forEach(function (el) {
            if (el.closest(".filter-field[hidden]") || el.value === "") {
                el.disabled = true;
            }
        });
    });

    // Bấm Back quay lại trang: bật lại các ô đã bị tắt lúc gửi form
    window.addEventListener("pageshow", function () {
        form.querySelectorAll("input:disabled, select:disabled").forEach(function (el) {
            el.disabled = false;
        });
    });

    refresh();
})();
