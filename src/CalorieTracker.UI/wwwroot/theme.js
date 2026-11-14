(function () {
    var key = "calorietracker.theme";

    function get() {
        try {
            var value = localStorage.getItem(key);
            return value === "light" || value === "dark" ? value : "system";
        } catch (e) {
            return "system";
        }
    }

    function apply(value) {
        if (value === "light" || value === "dark") {
            document.documentElement.setAttribute("data-theme", value);
        } else {
            document.documentElement.removeAttribute("data-theme");
        }
    }

    function set(value) {
        apply(value);
        try {
            localStorage.setItem(key, value);
            return true;
        } catch (e) {
            return false;
        }
    }

    apply(get());
    window.calorieTrackerTheme = { get: get, set: set };
})();
