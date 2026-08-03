const apologiaConversationThreads = new WeakMap();

window.apologiaStudio = {
    setDocumentLanguage(language) {
        if (language === "fr" || language === "en") {
            document.documentElement.lang = language;
        }
    },

    focusElementById(elementId) {
        const element = document.getElementById(elementId);

        if (element && element.offsetParent !== null) {
            element.focus();
        }
    },

    async copyText(text) {
        if (typeof text !== "string" || text.length === 0) {
            return false;
        }

        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(text);
                return true;
            }

            const textArea = document.createElement("textarea");
            textArea.value = text;
            textArea.setAttribute("readonly", "");
            textArea.style.position = "fixed";
            textArea.style.opacity = "0";
            document.body.appendChild(textArea);
            textArea.select();

            const copied = document.execCommand("copy");
            document.body.removeChild(textArea);

            return copied;
        } catch {
            return false;
        }
    },

    registerConversationThread(element, dotNetReference) {
        if (!element || !dotNetReference) {
            return;
        }

        this.unregisterConversationThread(element);

        let animationFrame = null;
        let lastNearBottom = null;

        const notify = () => {
            animationFrame = null;

            const distanceToBottom =
                element.scrollHeight -
                element.scrollTop -
                element.clientHeight;

            const isNearBottom = distanceToBottom <= 96;

            if (isNearBottom === lastNearBottom) {
                return;
            }

            lastNearBottom = isNearBottom;

            dotNetReference
                .invokeMethodAsync(
                    "SetConversationThreadNearBottom",
                    isNearBottom)
                .catch(() => {
                    // The Blazor circuit may have disconnected.
                });
        };

        const onScroll = () => {
            if (animationFrame !== null) {
                return;
            }

            animationFrame = window.requestAnimationFrame(notify);
        };

        element.addEventListener(
            "scroll",
            onScroll,
            { passive: true });

        apologiaConversationThreads.set(
            element,
            { onScroll, animationFrame: () => animationFrame });

        notify();
    },

    unregisterConversationThread(element) {
        if (!element) {
            return;
        }

        const registration =
            apologiaConversationThreads.get(element);

        if (!registration) {
            return;
        }

        element.removeEventListener(
            "scroll",
            registration.onScroll);

        const animationFrame = registration.animationFrame();

        if (animationFrame !== null) {
            window.cancelAnimationFrame(animationFrame);
        }

        apologiaConversationThreads.delete(element);
    },

    scrollConversationToEnd(element, behavior) {
        if (!element) {
            return;
        }

        element.scrollTo({
            top: element.scrollHeight,
            behavior: behavior === "smooth" ? "smooth" : "auto"
        });
    }
};
