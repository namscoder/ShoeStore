// Trang danh sách sản phẩm: tick ô lọc là lọc ngay, đổi sắp xếp là tải lại, nút "Bộ lọc" trên điện thoại
document.addEventListener("DOMContentLoaded", function () {
    var form = document.querySelector("[data-shop-filter]");
    if (!form) return;

    // Gửi form, bỏ các ô trống để địa chỉ trang gọn (vd: không có "MinPrice=&MaxPrice=")
    function submitClean() {
        form.querySelectorAll("input, select").forEach(function (el) {
            if ((el.type === "search" || el.type === "number") && el.value.trim() === "") {
                el.disabled = true;
            }
        });
        var emptyCategory = form.querySelector("input[name='CategoryId'][value='']:checked");
        if (emptyCategory) emptyCategory.disabled = true;

        // Sắp xếp mặc định (mới nhất) thì không cần ghi lên địa chỉ trang
        var sortSelect = document.querySelector("[data-shop-sort]");
        if (sortSelect && sortSelect.value === "newest") sortSelect.disabled = true;

        form.submit();
    }

    // Tick danh mục / thương hiệu / size / còn hàng => lọc ngay (ô giá thì bấm "Áp dụng")
    form.addEventListener("change", function (e) {
        if (e.target.matches("input[type='checkbox'], input[type='radio']")) {
            submitClean();
        }
    });

    form.addEventListener("submit", function (e) {
        e.preventDefault();
        submitClean();
    });

    // Ô "Sắp xếp" nằm ngoài form nhưng thuộc form (form="shopFilter")
    var sort = document.querySelector("[data-shop-sort]");
    if (sort) {
        sort.addEventListener("change", submitClean);
    }

    // Điện thoại: bấm "Bộ lọc" để mở / đóng cột lọc
    var toggle = document.querySelector("[data-shop-toggle]");
    var sidebar = document.querySelector("[data-shop-sidebar]");
    if (toggle && sidebar) {
        toggle.addEventListener("click", function () {
            sidebar.classList.toggle("open");
        });
    }

    // Bấm Back quay lại trang: bật lại các ô đã bị tắt lúc gửi form
    window.addEventListener("pageshow", function () {
        form.querySelectorAll(":disabled").forEach(function (el) {
            el.disabled = false;
        });
    });
});
