// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
    const rows = Array.from(document.querySelectorAll(".js-pending-product"));
    const trackerPage = document.querySelector("[data-pending-status-url]");
    const statusUrl = trackerPage?.dataset.pendingStatusUrl;

    if (rows.length === 0 || !statusUrl) {
        return;
    }

    const pollIntervalMilliseconds = 5000;
    const maximumAttempts = 30;
    let attempt = 0;

    function updateRow(row, product) {
        const nameElement = row.querySelector(".js-product-name");
        const priceElement = row.querySelector(".js-current-price");
        const checkedElement = row.querySelector(".js-last-checked");
        const statusElement = row.querySelector(".js-product-status");

        if (product.productName) {
            nameElement.textContent = product.productName;
            nameElement.title = product.productName;
        }

        priceElement.textContent = product.currentPrice
            ? `₺${product.currentPrice}`
            : "BEKLENİYOR";

        checkedElement.textContent = product.lastChecked || "—";

        if (product.isReady) {
            statusElement.innerHTML =
                '<span class="status-badge status-active"><span class="status-dot"></span> AKTİF</span>';
            row.classList.remove("js-pending-product");
        }
    }

    async function checkPendingProducts() {
        attempt += 1;

        for (const row of rows) {
            if (!row.classList.contains("js-pending-product")) {
                continue;
            }

            const productId = row.dataset.productId;

            try {
                const response = await fetch(
                    `${statusUrl}?Id=${encodeURIComponent(productId)}`,
                    { headers: { Accept: "application/json" } });

                if (!response.ok) {
                    continue;
                }

                const product = await response.json();
                updateRow(row, product);
            } catch {
                // A temporary request failure should not break the page.
            }
        }

        const stillPending = rows.some(row =>
            row.classList.contains("js-pending-product"));

        if (stillPending && attempt < maximumAttempts) {
            window.setTimeout(checkPendingProducts, pollIntervalMilliseconds);
        }
    }

    window.setTimeout(checkPendingProducts, pollIntervalMilliseconds);
})();
