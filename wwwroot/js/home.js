// Lọc sản phẩm mới theo danh mục ngay trên trang chủ
document.addEventListener("DOMContentLoaded", function () {
    var tabs = document.querySelectorAll(".hm-tab");
    var cards = document.querySelectorAll("#san-pham-moi .hm-card");
    var emptyMsg = document.querySelector(".hm-grid-empty");

    function applyFilter(value) {
        var visible = 0;

        tabs.forEach(function (tab) {
            tab.classList.toggle("active", tab.dataset.filter === value);
        });

        cards.forEach(function (card) {
            var show = value === "all" || card.dataset.category === value;
            card.hidden = !show;
            if (show) visible++;
        });

        if (emptyMsg) emptyMsg.hidden = visible > 0;
    }

    tabs.forEach(function (tab) {
        tab.addEventListener("click", function () {
            applyFilter(tab.dataset.filter);
        });
    });

    // Bấm thẻ danh mục ở trên -> cuộn xuống và lọc theo danh mục đó
    document.querySelectorAll("[data-filter-link]").forEach(function (link) {
        link.addEventListener("click", function () {
            applyFilter(link.dataset.filterLink);
        });
    });
});
