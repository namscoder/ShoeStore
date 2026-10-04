// Quản lý các dòng size / màu / số lượng trong form thêm và sửa sản phẩm
(function () {
    const tbody = document.getElementById("variantRows");
    const template = document.getElementById("variantRowTemplate");
    const addButton = document.getElementById("addVariantRow");

    if (!tbody || !template || !addButton) {
        return;
    }

    // Đánh lại số thứ tự [0], [1], [2]... để server nhận đúng danh sách
    function reindexRows() {
        const rows = tbody.querySelectorAll(".variant-row");

        rows.forEach(function (row, index) {
            row.querySelectorAll("[name]").forEach(function (el) {
                el.name = el.name.replace(/ProductVariants\[[^\]]*\]/, "ProductVariants[" + index + "]");
            });

            row.querySelectorAll("[data-valmsg-for]").forEach(function (el) {
                const target = el.getAttribute("data-valmsg-for")
                    .replace(/ProductVariants\[[^\]]*\]/, "ProductVariants[" + index + "]");
                el.setAttribute("data-valmsg-for", target);
            });
        });

        // Chỉ còn 1 dòng thì không cho xoá
        tbody.querySelectorAll(".btn-remove-variant").forEach(function (button) {
            button.disabled = rows.length <= 1;
        });
    }

    // Bấm "Thêm size / màu": sao chép dòng mẫu và thêm vào cuối bảng
    addButton.addEventListener("click", function () {
        const newRow = template.content.querySelector(".variant-row").cloneNode(true);
        tbody.appendChild(newRow);
        reindexRows();
    });

    // Bấm nút thùng rác: xoá dòng chứa nút đó
    tbody.addEventListener("click", function (event) {
        const button = event.target.closest(".btn-remove-variant");
        if (!button) {
            return;
        }

        button.closest(".variant-row").remove();
        reindexRows();
    });

    reindexRows();
})();