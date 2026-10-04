// Hộp chi tiết sản phẩm (trang danh sách admin): bấm ảnh nhỏ => hiện ảnh đó ở khung ảnh lớn
document.addEventListener("click", function (event) {
    const thumb = event.target.closest("[data-gallery-src]");
    if (!thumb) {
        return;
    }

    const modal = thumb.closest(".modal");
    const mainImage = modal.querySelector('[data-role="main-image"]');
    if (mainImage) {
        mainImage.src = thumb.dataset.gallerySrc;
    }

    // Viền xanh cho ảnh nhỏ đang được chọn
    modal.querySelectorAll("[data-gallery-src]").forEach(function (item) {
        item.classList.toggle("border-primary", item === thumb);
    });
});
