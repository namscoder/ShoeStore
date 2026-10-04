// Trang tài khoản: hiện/ẩn mật khẩu, xoá địa chỉ mặc định, chặn bấm Lưu 2 lần
document.addEventListener("DOMContentLoaded", function () {
    // Nút "Hiện" / "Ẩn" bên cạnh ô mật khẩu
    document.querySelectorAll("[data-toggle-password]").forEach(function (button) {
        button.addEventListener("click", function () {
            var input = button.parentElement.querySelector("input");
            var show = input.type === "password";
            input.type = show ? "text" : "password";
            button.textContent = show ? "Ẩn" : "Hiện";
            button.setAttribute("aria-label", show ? "Ẩn mật khẩu" : "Hiện mật khẩu");
        });
    });

    // "Xoá địa chỉ mặc định": bỏ chọn tỉnh, phường và xoá số nhà => bấm Lưu thì server xoá địa chỉ
    var clearButton = document.querySelector("[data-clear-address]");
    if (clearButton) {
        clearButton.addEventListener("click", function () {
            var picker = clearButton.closest("[data-address-picker]");
            var province = picker.querySelector("[data-province]");
            var street = picker.querySelector("[data-street]");
            var manual = picker.querySelector("[data-manual-area] textarea");

            province.value = "";
            province.dispatchEvent(new Event("change")); // address-picker.js tự làm trống ô phường/xã
            street.value = "";
            street.dispatchEvent(new Event("input"));
            if (manual) manual.value = "";

            clearButton.textContent = "Đã bỏ địa chỉ — bấm \"Lưu thay đổi\" để xác nhận";
            clearButton.disabled = true;
        });
    }

    // Chặn bấm nút gửi 2 lần liên tiếp (chỉ khi form đã hợp lệ)
    document.querySelectorAll("form").forEach(function (form) {
        var submit = form.querySelector("[data-submit]");
        if (!submit) return;

        form.addEventListener("submit", function () {
            if (!window.jQuery || $(form).valid()) {
                submit.disabled = true;
                submit.textContent = "Đang lưu...";
            }
        });
    });
});
