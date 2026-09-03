const apologiaConversationThreads = new WeakMap();
const apologiaComposerEnterHandlers = new WeakMap();

const apologiaDefaultTheme = {
    mode: "light",
    color: "#2D766E",
    darkPageColor: "#242424",
    darkSurfaceColor: "#303030"
};

const apologiaLightPage = "#FBFAF7";

const apologiaLightSurface = "#FFFFFF";

const apologiaDarkShadeRange = {
    minimum: 16,
    maximum: 88
};

const apologiaDarkPaletteProperties = [
    "--apologia-page",
    "--apologia-surface",
    "--apologia-surface-muted",
    "--apologia-surface-hover",
    "--apologia-border",
    "--apologia-border-strong"
];

function normalizeThemeColor(color) {
    if (typeof color !== "string" ||
        !/^#[0-9a-f]{6}$/i.test(color.trim())) {
        return apologiaDefaultTheme.color;
    }

    return color.trim().toUpperCase();
}

function hexToRgb(color) {
    const value = Number.parseInt(color.substring(1), 16);
    return {
        red: (value >> 16) & 255,
        green: (value >> 8) & 255,
        blue: value & 255
    };
}

function rgbToHex({ red, green, blue }) {
    return `#${[red, green, blue]
        .map(channel => Math.round(channel)
            .toString(16)
            .padStart(2, "0"))
        .join("")}`.toUpperCase();
}

function normalizeDarkShade(color, fallback) {
    if (typeof color !== "string" ||
        !/^#[0-9a-f]{6}$/i.test(color.trim())) {
        return fallback;
    }

    const { red, green, blue } = hexToRgb(color.trim());
    const channel = Math.min(
        apologiaDarkShadeRange.maximum,
        Math.max(
            apologiaDarkShadeRange.minimum,
            Math.round((red + green + blue) / 3)));

    return rgbToHex({
        red: channel,
        green: channel,
        blue: channel
    });
}

function mixColors(color, target, targetRatio) {
    return rgbToHex({
        red: color.red + (target.red - color.red) * targetRatio,
        green: color.green + (target.green - color.green) * targetRatio,
        blue: color.blue + (target.blue - color.blue) * targetRatio
    });
}

function relativeLuminance(color) {
    const channels = [color.red, color.green, color.blue]
        .map(channel => {
            const value = channel / 255;
            return value <= 0.04045
                ? value / 12.92
                : Math.pow((value + 0.055) / 1.055, 2.4);
        });

    return 0.2126 * channels[0] +
        0.7152 * channels[1] +
        0.0722 * channels[2];
}

function contrastRatio(first, second) {
    const lightest = Math.max(
        relativeLuminance(first),
        relativeLuminance(second));
    const darkest = Math.min(
        relativeLuminance(first),
        relativeLuminance(second));
    return (lightest + 0.05) / (darkest + 0.05);
}

// The screen and the presentation areas are configured separately, so an
// accent readable on one is not automatically readable on the other: every
// candidate has to clear 4.5:1 against both backgrounds.
function readableAccent(color, backgrounds, mode) {
    const isReadable = candidate => backgrounds.every(
        background => contrastRatio(candidate, background) >= 4.5);

    if (isReadable(color)) {
        return rgbToHex(color);
    }

    const target = mode === "dark"
        ? { red: 255, green: 255, blue: 255 }
        : { red: 0, green: 0, blue: 0 };

    for (let ratio = 0.08; ratio <= 1; ratio += 0.04) {
        const candidate = hexToRgb(
            mixColors(color, target, ratio));
        if (isReadable(candidate)) {
            return rgbToHex(candidate);
        }
    }

    return mode === "dark" ? "#FFFFFF" : "#000000";
}

function applyDarkPalette(root, pageColor, surfaceColor) {
    const surface = hexToRgb(surfaceColor);
    const white = { red: 255, green: 255, blue: 255 };

    root.style.setProperty("--apologia-page", pageColor);
    root.style.setProperty("--apologia-surface", surfaceColor);
    root.style.setProperty(
        "--apologia-surface-muted",
        mixColors(surface, white, 0.06));
    root.style.setProperty(
        "--apologia-surface-hover",
        mixColors(surface, white, 0.12));
    root.style.setProperty(
        "--apologia-border",
        mixColors(surface, white, 0.18));
    root.style.setProperty(
        "--apologia-border-strong",
        mixColors(surface, white, 0.32));
}

function applyTheme(
    mode,
    color,
    darkPageColor,
    darkSurfaceColor,
    persist = true) {
    const normalizedMode = mode === "dark" ? "dark" : "light";
    const normalizedColor = normalizeThemeColor(color);
    const normalizedPage = normalizeDarkShade(
        darkPageColor,
        apologiaDefaultTheme.darkPageColor);
    const normalizedSurface = normalizeDarkShade(
        darkSurfaceColor,
        apologiaDefaultTheme.darkSurfaceColor);
    const accent = hexToRgb(normalizedColor);
    const white = { red: 255, green: 255, blue: 255 };
    const black = { red: 0, green: 0, blue: 0 };
    const page = hexToRgb(
        normalizedMode === "dark"
            ? normalizedPage
            : apologiaLightPage);
    // Accent tints back chips, badges and selections, which all sit on the
    // presentation areas: they blend into the surface, not into the screen.
    const surface = hexToRgb(
        normalizedMode === "dark"
            ? normalizedSurface
            : apologiaLightSurface);
    const root = document.documentElement;

    root.dataset.theme = normalizedMode;
    root.style.colorScheme = normalizedMode;

    if (normalizedMode === "dark") {
        applyDarkPalette(root, normalizedPage, normalizedSurface);
    } else {
        apologiaDarkPaletteProperties.forEach(property =>
            root.style.removeProperty(property));
    }

    root.style.setProperty("--apologia-accent", normalizedColor);
    root.style.setProperty(
        "--apologia-accent-strong",
        mixColors(
            accent,
            normalizedMode === "dark" ? white : black,
            0.18));
    root.style.setProperty(
        "--apologia-accent-soft",
        mixColors(accent, surface, normalizedMode === "dark" ? 0.78 : 0.88));
    root.style.setProperty(
        "--apologia-accent-border",
        mixColors(accent, surface, normalizedMode === "dark" ? 0.45 : 0.58));
    root.style.setProperty(
        "--apologia-accent-text",
        readableAccent(accent, [page, surface], normalizedMode));
    root.style.setProperty(
        "--apologia-on-accent",
        contrastRatio(accent, white) >= contrastRatio(accent, black)
            ? "#FFFFFF"
            : "#111411");

    if (persist) {
        try {
            window.localStorage.setItem(
                "Apologia.Theme",
                JSON.stringify({
                    mode: normalizedMode,
                    color: normalizedColor,
                    darkPageColor: normalizedPage,
                    darkSurfaceColor: normalizedSurface
                }));
        } catch {
            // The database remains the source of truth.
        }
    }
}

function applyDefaultTheme() {
    applyTheme(
        apologiaDefaultTheme.mode,
        apologiaDefaultTheme.color,
        apologiaDefaultTheme.darkPageColor,
        apologiaDefaultTheme.darkSurfaceColor,
        false);
}

async function synchronizeTheme() {
    try {
        const response = await fetch(
            "/api/preferences/theme",
            {
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });

        if (!response.ok) {
            if (response.status === 401 || response.status === 403) {
                applyDefaultTheme();
            }
            return;
        }

        const contentType = response.headers.get("content-type") ?? "";
        if (!contentType.includes("application/json")) {
            applyDefaultTheme();
            return;
        }

        const theme = await response.json();
        applyTheme(
            theme.mode,
            theme.color,
            theme.darkPageColor,
            theme.darkSurfaceColor);
    } catch {
        // Keep the cached or default theme while offline.
    }
}

try {
    const cachedTheme = JSON.parse(
        window.localStorage.getItem("Apologia.Theme"));
    applyTheme(
        cachedTheme?.mode,
        cachedTheme?.color,
        cachedTheme?.darkPageColor,
        cachedTheme?.darkSurfaceColor,
        false);
} catch {
    applyDefaultTheme();
}

function startThemeSynchronization() {
    synchronizeTheme();

    if (window.Blazor?.addEventListener) {
        window.Blazor.addEventListener(
            "enhancedload",
            synchronizeTheme);
    }
}

if (document.readyState === "loading") {
    document.addEventListener(
        "DOMContentLoaded",
        startThemeSynchronization,
        { once: true });
} else {
    startThemeSynchronization();
}

window.addEventListener("pageshow", synchronizeTheme);

window.apologiaStudio = {
    applyTheme,

    setDocumentLanguage(language) {
        if (language === "fr" || language === "en") {
            document.documentElement.lang = language;
            document.cookie = `Apologia.InterfaceLanguage=${encodeURIComponent(language)}; Path=/; Max-Age=31536000; SameSite=Lax`;
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

    registerComposerEnterBehavior(textArea, sendButton, sendOnEnter) {
        if (!textArea || !sendButton) {
            return;
        }

        this.unregisterComposerEnterBehavior(textArea);

        if (!sendOnEnter) {
            return;
        }

        const onKeyDown = event => {
            if (event.key !== "Enter" ||
                event.ctrlKey ||
                event.isComposing) {
                return;
            }

            event.preventDefault();

            if (event.repeat || sendButton.disabled) {
                return;
            }

            sendButton.click();
        };

        textArea.addEventListener("keydown", onKeyDown);
        apologiaComposerEnterHandlers.set(textArea, onKeyDown);
    },

    unregisterComposerEnterBehavior(textArea) {
        if (!textArea) {
            return;
        }

        const onKeyDown =
            apologiaComposerEnterHandlers.get(textArea);

        if (!onKeyDown) {
            return;
        }

        textArea.removeEventListener("keydown", onKeyDown);
        apologiaComposerEnterHandlers.delete(textArea);
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
