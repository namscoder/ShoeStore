// Trang chi tiết sản phẩm: chọn màu + size, kiểm tra tồn kho, chọn số lượng
document.addEventListener("DOMContentLoaded", function () {
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

    // Giỏ hàng chưa làm (cần đăng nhập) -> tạm thời chỉ báo lại lựa chọn
    form.addEventListener("submit", function (e) {
        e.preventDefault();
        if (addButton.disabled) return;

        note.textContent = "Bạn đã chọn " + qtyInput.value + " đôi, màu " + colorLabel.textContent +
            ", size " + sizeLabel.textContent + ". Chức năng giỏ hàng sẽ có khi làm phần đăng nhập.";
        note.hidden = false;
    });

    // Chỉ có 1 màu hoặc 1 size thì chọn sẵn
    if (colorButtons.length === 1) selectedColor = Number(colorButtons[0].dataset.colorId);
    if (sizeButtons.length === 1) selectedSize = Number(sizeButtons[0].dataset.sizeId);

    render();
});
