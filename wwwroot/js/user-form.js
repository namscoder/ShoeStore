// Form thêm/sửa tài khoản (admin): hỏi xác nhận khi cấp quyền Quản trị viên cho một tài khoản
(function () {
    const form = document.getElementById("userForm");
    if (!form) {
        return;
    }

    form.addEventListener("submit", function (event) {
        const role = form.querySelector('[name="Role"]');
        const becomingAdmin = role && role.value === "Admin" && role.dataset.originalRole !== "Admin";

        if (becomingAdmin && $(form).valid() && !confirm(
            "Cấp quyền Quản trị viên: tài khoản này sẽ được toàn quyền quản lý cửa hàng " +
            "(thêm, sửa, xoá sản phẩm, khoá tài khoản...). Tiếp tục?")) {
            event.preventDefault();
        }
    });
})();
