window.apologiaStudio = {
    setDocumentLanguage(language) {
        if (language === "fr" || language === "en") {
            document.documentElement.lang = language;
        }
    }
};
