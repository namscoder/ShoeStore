// Chọn địa chỉ giao hàng kiểu Shopee:
// Tỉnh/Thành phố -> Phường/Xã (đơn vị hành chính sau sáp nhập 07/2025, không còn cấp quận/huyện) + số nhà, tên đường.
// Dữ liệu lấy từ provinces.open-api.vn. Lỗi tải dữ liệu thì tự chuyển sang ô nhập tay để khách vẫn đặt được hàng.
(function () {
    const picker = document.querySelector("[data-address-picker]");
    if (!picker) {
        return;
    }

    const api = picker.dataset.api;
    const provinceSelect = picker.querySelector("[data-province]");
    const wardSelect = picker.querySelector("[data-ward]");
    const streetInput = picker.querySelector("[data-street]");
    const modeInput = picker.querySelector("input[name='AddressMode']");
    const pickerArea = picker.querySelector("[data-picker-area]");
    const manualArea = picker.querySelector("[data-manual-area]");
    const preview = picker.querySelector("[data-address-preview]");

    const CACHE_DAYS = 7;

    // ===== Lưu tạm dữ liệu trong trình duyệt (danh sách hành chính rất ít khi đổi) =====
    function cacheGet(key) {
        try {
            const item = JSON.parse(localStorage.getItem(key));
            return item && Date.now() - item.time < CACHE_DAYS * 24 * 3600 * 1000 ? item.data : null;
        } catch (e) {
            return null;
        }
    }

    function cacheSet(key, data) {
        try {
            localStorage.setItem(key, JSON.stringify({ time: Date.now(), data: data }));
        } catch (e) {
            // Trình duyệt chặn lưu (chế độ ẩn danh...) thì bỏ qua, lần sau tải lại
        }
    }

    async function getJson(path, cacheKey) {
        const cached = cacheGet(cacheKey);
        if (cached) {
            return cached;
        }

        const response = await fetch(api + path);
        if (!response.ok) {
            throw new Error("HTTP " + response.status);
        }

        const data = await response.json();
        cacheSet(cacheKey, data);
        return data;
    }

    // Sắp xếp A-Z theo tên bỏ tiền tố: "Tỉnh An Giang" -> "An Giang", "Phường Bến Thành" -> "Bến Thành"
    function sortByName(items) {
        const bare = function (name) {
            return name.replace(/^(Thành phố|Tỉnh|Phường|Xã|Đặc khu)\s+/, "");
        };
        return items.slice().sort(function (a, b) {
            return bare(a.name).localeCompare(bare(b.name), "vi");
        });
    }

    // Đổ danh sách vào ô chọn. Giá trị gửi lên server là TÊN (để ghép thành địa chỉ), mã nằm ở data-code
    function fillSelect(select, items, placeholder, selectedName) {
        select.innerHTML = "";
        select.appendChild(new Option(placeholder, ""));

        items.forEach(function (item) {
            const option = new Option(item.name, item.name, false, item.name === selectedName);
            option.dataset.code = item.code;
            select.appendChild(option);
        });

        select.disabled = items.length === 0;
    }

    // Xem trước địa chỉ đầy đủ sẽ lưu vào đơn hàng
    function updatePreview() {
        const parts = [streetInput.value.trim(), wardSelect.value, provinceSelect.value].filter(Boolean);
        preview.hidden = parts.length === 0;
        // Trang nào muốn chữ khác thì đặt data-preview-label (vd: trang Tài khoản)
        preview.textContent = (picker.dataset.previewLabel || "Giao tới: ") + parts.join(", ");
    }

    async function loadWards(selectedWardName) {
        const option = provinceSelect.selectedOptions[0];

        if (!option || !option.dataset.code) {
            fillSelect(wardSelect, [], "Chọn phường/xã", "");
            updatePreview();
            return;
        }

        wardSelect.disabled = true;
        wardSelect.innerHTML = '<option value="">Đang tải phường/xã...</option>';

        try {
            const code = option.dataset.code;
            const data = await getJson("/p/" + code + "?depth=2", "shoestore-wards-" + code);
            fillSelect(wardSelect, sortByName(data.wards || []), "Chọn phường/xã", selectedWardName);
        } catch (e) {
            switchToManual();
        }

        updatePreview();
    }

    // Chuyển sang nhập tay: tắt (disabled) các ô chọn để không bị kiểm tra và không gửi lên server
    function switchToManual() {
        modeInput.value = "manual";
        pickerArea.hidden = true;
        pickerArea.querySelectorAll("select, input").forEach(function (el) {
            el.disabled = true;
        });
        manualArea.hidden = false;
        manualArea.querySelector("textarea").disabled = false;
    }

    provinceSelect.addEventListener("change", function () {
        loadWards("");
    });
    wardSelect.addEventListener("change", updatePreview);
    streetInput.addEventListener("input", updatePreview);

    (async function init() {
        // Lần trước đã ở chế độ nhập tay (gửi form bị lỗi) thì giữ nguyên
        if (modeInput.value === "manual") {
            switchToManual();
            return;
        }

        try {
            const provinces = await getJson("/p/", "shoestore-provinces");
            fillSelect(provinceSelect, sortByName(provinces), "Chọn tỉnh/thành phố", provinceSelect.dataset.selected);

            if (provinceSelect.value) {
                await loadWards(wardSelect.dataset.selected);
            }
        } catch (e) {
            switchToManual();
        }

        updatePreview();
    })();
})();
