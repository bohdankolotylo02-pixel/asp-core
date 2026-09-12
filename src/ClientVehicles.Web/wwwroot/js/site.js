/* ---------------------------------------------------------------------------
   Clients & Vehicles - small vanilla script, no libraries.
   Three jobs:
     1. in-app confirmation dialog (no native confirm())
     2. lightweight client side checks, mirroring the server rules
     3. guard against double submits
   The server re-validates everything; this file is only about feedback speed.
   --------------------------------------------------------------------------- */
(function () {
    "use strict";

    /* ------------------------------- confirm ------------------------------ */

    var modal = document.getElementById("confirm-modal");
    var modalText = document.getElementById("confirm-modal-text");
    var modalAccept = document.getElementById("confirm-modal-accept");
    var pendingForm = null;
    var lastFocused = null;

    function openConfirm(form) {
        if (!modal) {
            form.submit();
            return;
        }

        pendingForm = form;
        lastFocused = document.activeElement;
        modalText.textContent = form.getAttribute("data-confirm") || "Are you sure?";
        modalAccept.textContent = form.getAttribute("data-confirm-label") || "Confirm";
        modalAccept.className = form.getAttribute("data-confirm-kind") === "safe"
            ? "btn btn--primary"
            : "btn btn--danger";
        modal.hidden = false;
        modalAccept.focus();
    }

    function closeConfirm() {
        if (!modal) {
            return;
        }

        modal.hidden = true;
        pendingForm = null;
        if (lastFocused && typeof lastFocused.focus === "function") {
            lastFocused.focus();
        }
    }

    if (modal) {
        modal.addEventListener("click", function (event) {
            if (event.target.hasAttribute("data-confirm-cancel")) {
                closeConfirm();
            }
        });

        modalAccept.addEventListener("click", function () {
            if (!pendingForm) {
                return;
            }

            var form = pendingForm;
            pendingForm = null;
            modal.hidden = true;
            lockSubmit(form);
            form.submit();
        });

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape" && !modal.hidden) {
                closeConfirm();
            }
        });
    }

    /* ------------------------------ validation ---------------------------- */

    function messageFor(input) {
        return input.getAttribute("data-msg-required")
            || (labelTextFor(input) || "This field") + " is required.";
    }

    function labelTextFor(input) {
        var label = input.form ? input.form.querySelector('label[for="' + input.id + '"]') : null;
        return label ? label.textContent.replace("*", "").trim() : null;
    }

    function errorSlot(input) {
        if (!input.form) {
            return null;
        }

        return input.form.querySelector('[data-valmsg-for="' + input.name + '"]');
    }

    function setError(input, message) {
        var slot = errorSlot(input);
        if (slot) {
            slot.textContent = message;
        }

        if (message) {
            input.classList.add("input-error");
        } else {
            input.classList.remove("input-error");
        }
    }

    function checkInput(input) {
        if (input.disabled || input.closest("[hidden]")) {
            return true;
        }

        var value = (input.value || "").trim();

        if (input.hasAttribute("required") && value === "") {
            setError(input, messageFor(input));
            return false;
        }

        if (input.type === "number" && value !== "") {
            var numeric = Number(value);
            if (isNaN(numeric)) {
                setError(input, "Enter a number.");
                return false;
            }

            var min = input.getAttribute("min");
            if (min !== null && numeric < Number(min)) {
                setError(input, "Value cannot be lower than " + min + ".");
                return false;
            }
        }

        setError(input, "");
        return true;
    }

    function lockSubmit(form) {
        form.setAttribute("data-submitting", "true");
        var buttons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
        Array.prototype.forEach.call(buttons, function (button) {
            button.disabled = true;
            if (button.tagName === "BUTTON" && button.getAttribute("data-busy-label")) {
                button.textContent = button.getAttribute("data-busy-label");
            }
        });
    }

    document.addEventListener("submit", function (event) {
        var form = event.target;

        // Double submit guard: the second submit of the same form is dropped.
        if (form.getAttribute("data-submitting") === "true") {
            event.preventDefault();
            return;
        }

        if (form.hasAttribute("data-confirm")) {
            event.preventDefault();
            openConfirm(form);
            return;
        }

        if (form.hasAttribute("data-validate")) {
            var inputs = form.querySelectorAll("input[name], select[name], textarea[name]");
            var firstInvalid = null;

            Array.prototype.forEach.call(inputs, function (input) {
                if (!checkInput(input) && !firstInvalid) {
                    firstInvalid = input;
                }
            });

            if (firstInvalid) {
                event.preventDefault();
                firstInvalid.focus();
                return;
            }
        }

        lockSubmit(form);
    }, true);

    document.addEventListener("input", function (event) {
        var input = event.target;
        if (input.classList && input.classList.contains("input-error")) {
            checkInput(input);
        }
    });

    /* --------------------------- optional sections ------------------------ */

    // "Add first vehicle" on the new client page: reveals the block and turns its
    // required flags on and off, so a hidden block can never block the form.
    Array.prototype.forEach.call(document.querySelectorAll("[data-toggle-target]"), function (toggle) {
        var target = document.getElementById(toggle.getAttribute("data-toggle-target"));
        if (!target) {
            return;
        }

        var apply = function () {
            target.hidden = !toggle.checked;
            Array.prototype.forEach.call(target.querySelectorAll("[data-required-when-shown]"), function (input) {
                if (toggle.checked) {
                    input.setAttribute("required", "required");
                } else {
                    input.removeAttribute("required");
                    setError(input, "");
                }
            });
        };

        toggle.addEventListener("change", apply);
        apply();
    });
})();
