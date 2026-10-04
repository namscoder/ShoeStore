// Trang chi tiết sản phẩm: thư viện ảnh, chọn màu + size, kiểm tra tồn kho, chọn số lượng
document.addEventListener("DOMContentLoaded", function () {
    // ===================== Thư viện ảnh =====================
    var mainImage = document.querySelector("[data-gallery-main]");
    var thumbsBox = document.querySelector("[data-gallery-thumbs]");
    var galleryColor = null; // màu đang ưu tiên trong dãy ảnh nhỏ

    function allThumbs() {
        return thumbsBox ? Array.prototype.slice.call(thumbsBox.children) : [];
    }

    // Hiện 1 ảnh nhỏ lên khung ảnh lớn
    function showThumb(thumb) {
        if (!thumb || !mainImage) return;
        mainImage.src = thumb.dataset.src;
        allThumbs().forEach(function (t) {
            t.classList.toggle("active", t === thumb);
        });
    }

    // Chọn màu: ảnh của màu đó dồn lên đầu dãy + hiện ảnh đầu tiên của màu lên khung lớn.
    // Bỏ chọn màu (colorId = null): trả dãy ảnh về thứ tự ban đầu.
    function showColorImages(colorId) {
        if (!thumbsBox || colorId === galleryColor) return;
        galleryColor = colorId;

        var byOrder = allThumbs().sort(function (a, b) {
            return Number(a.dataset.order) - Number(b.dataset.order);
        });

        var ofColor = colorId === null ? [] : byOrder.filter(function (t) {
            return t.dataset.thumbColor === String(colorId);
        });

        ofColor.concat(byOrder.filter(function (t) { return ofColor.indexOf(t) < 0; }))
            .forEach(function (t) { thumbsBox.appendChild(t); }); // appendChild với phần tử có sẵn = di chuyển

        if (ofColor.length > 0) {
            showThumb(ofColor[0]);
        } else if (colorId === null) {
            showThumb(byOrder[0]);
        }
        // Màu không có ảnh riêng thì giữ nguyên ảnh đang xem

        thumbsBox.scrollLeft = 0;
    }

    if (thumbsBox) {
        thumbsBox.addEventListener("click", function (e) {
            showThumb(e.target.closest(".pd-thumb"));
        });
    }

    // ===================== Chọn màu / size =====================
    var form = document.querySelector(".pd-form");
    if (!form) return;

    // [{ id, sizeId, colorId, qty }]
    var variants = JSON.parse(form.dataset.variants || "[]");

    var colorButtons = form.querySelectorAll("[data-color-id]");
    var sizeButtons = form.querySelectorAll("[data-size-id]");
    var colorLabel = form.querySelector("[data-color-label]");
    var sizeLabel = form.querySelector("[data-size-label]");
    var stockText = form.querySelector("[data-stock]");
    var qtyInput = form.querySelector("[data-qty]");
    var addButton = form.querySelector("[data-add]");
    var note = form.querySelector("[data-note]");
    var variantInput = form.querySelector("input[name='ProductVariantId']");

    var selectedColor = null;
    var selectedSize = null;

    function findVariant(colorId, sizeId) {
        return variants.find(function (v) {
            return v.colorId === colorId && v.sizeId === sizeId;
        });
    }

    // Size còn hàng không? (nếu chưa chọn màu thì xét tất cả màu)
    function sizeAvailable(sizeId) {
        return variants.some(function (v) {
            return v.sizeId === sizeId && v.qty > 0 && (selectedColor === null || v.colorId === selectedColor);
        });
    }

    // Màu còn hàng không? (nếu đã chọn size thì xét đúng size đó)
    function colorAvailable(colorId) {
        return variants.some(function (v) {
            return v.colorId === colorId && v.qty > 0 && (selectedSize === null || v.sizeId === selectedSize);
        });
    }

    function render() {
        colorButtons.forEach(function (btn) {
            var id = Number(btn.dataset.colorId);
            btn.classList.toggle("active", id === selectedColor);
            btn.classList.toggle("unavailable", !colorAvailable(id));
        });

        // Ảnh của màu đang chọn lên đầu (chỉ đổi khi màu thay đổi, chọn size không ảnh hưởng)
        showColorImages(selectedColor);

        sizeButtons.forEach(function (btn) {
            var id = Number(btn.dataset.sizeId);
            btn.classList.toggle("active", id === selectedSize);
            btn.classList.toggle("unavailable", !sizeAvailable(id));
        });

        var colorBtn = form.querySelector("[data-color-id='" + selectedColor + "']");
        var sizeBtn = form.querySelector("[data-size-id='" + selectedSize + "']");
        colorLabel.textContent = colorBtn ? colorBtn.dataset.name : "Chưa chọn";
        sizeLabel.textContent = sizeBtn ? sizeBtn.dataset.name : "Chưa chọn";

        note.hidden = true;

        var variant = selectedColor !== null && selectedSize !== null
            ? findVariant(selectedColor, selectedSize)
            : null;

        stockText.classList.remove("ok", "low", "out");

        if (selectedColor === null || selectedSize === null) {
            stockText.textContent = "Chọn màu và size để xem tình trạng hàng.";
            setBuyable(null);
        } else if (!variant || variant.qty <= 0) {
            stockText.textContent = "Màu và size này đã hết hàng.";
            stockText.classList.add("out");
            setBuyable(null);
        } else {
            stockText.textContent = variant.qty <= 5
                ? "Chỉ còn " + variant.qty + " đôi — nhanh tay nhé!"
                : "Còn hàng (" + variant.qty + " đôi)";
            stockText.classList.add(variant.qty <= 5 ? "low" : "ok");
            setBuyable(variant);
        }
    }

    function setBuyable(variant) {
        addButton.disabled = !variant;
        variantInput.value = variant ? variant.id : "";
        qtyInput.max = variant ? variant.qty : 1;
        if (Number(qtyInput.value) > Number(qtyInput.max)) qtyInput.value = qtyInput.max;
        if (Number(qtyInput.value) < 1) qtyInput.value = 1;
    }

    colorButtons.forEach(function (btn) {
        btn.addEventListener("click", function () {
            var id = Number(btn.dataset.colorId);
            selectedColor = selectedColor === id ? null : id;
            render();
        });
    });

    sizeButtons.forEach(function (btn) {
        btn.addEventListener("click", function () {
            var id = Number(btn.dataset.sizeId);
            selectedSize = selectedSize === id ? null : id;
            render();
        });
    });

    form.querySelector("[data-qty-minus]").addEventListener("click", function () {
        qtyInput.value = Math.max(1, Number(qtyInput.value) - 1);
    });

    form.querySelector("[data-qty-plus]").addEventListener("click", function () {
        qtyInput.value = Math.min(Number(qtyInput.max), Number(qtyInput.value) + 1);
    });

    qtyInput.addEventListener("change", function () {
        var value = Math.round(Number(qtyInput.value) || 1);
        qtyInput.value = Math.min(Math.max(1, value), Number(qtyInput.max));
    });

    // Thêm vào giỏ: gửi bằng fetch để không phải tải lại trang
    form.addEventListener("submit", function (e) {
        e.preventDefault();
        if (addButton.disabled) return;

        addButton.disabled = true;

        fetch(form.action, {
            method: "POST",
            body: new FormData(form), // gồm cả mã chống giả mạo (__RequestVerificationToken)
            headers: { "X-Requested-With": "XMLHttpRequest" }
        })
            .then(function (response) { return response.json(); })
            .then(function (data) {
                // Chưa đăng nhập => chuyển sang trang đăng nhập, xong quay lại trang này
                if (data.requireLogin) {
                    window.location.href = data.loginUrl;
                    return;
                }

                note.textContent = data.message + " ";
                if (data.success) {
                    var link = document.createElement("a");
                    link.href = data.cartUrl;
                    link.textContent = "Xem giỏ hàng →";
                    note.appendChild(link);

                    // Cập nhật số trên biểu tượng giỏ hàng ở header
                    document.querySelectorAll("[data-cart-count]").forEach(function (badge) {
                        badge.textContent = data.cartCount;
                        badge.hidden = data.cartCount <= 0;
                    });
                }
                note.hidden = false;
            })
            .catch(function () {
                note.textContent = "Có lỗi xảy ra, vui lòng thử lại.";
                note.hidden = false;
            })
            .finally(function () {
                addButton.disabled = false;
            });
    });

    // Chỉ có 1 màu hoặc 1 size thì chọn sẵn
    if (colorButtons.length === 1) selectedColor = Number(colorButtons[0].dataset.colorId);
    if (sizeButtons.length === 1) selectedSize = Number(sizeButtons[0].dataset.sizeId);

    render();
});
