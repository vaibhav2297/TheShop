document.addEventListener("keydown", event => {
    if (event.key === "Enter" && !event.isComposing &&
        event.target instanceof HTMLInputElement && event.target.hasAttribute("data-shop-numeric")) {
        event.preventDefault();
    }
});
