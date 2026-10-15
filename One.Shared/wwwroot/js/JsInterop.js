// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export function showPrompt(message) {
    return prompt(message, 'Type anything here');
}

export function setDocumentLanguage(language, direction) {
    document.documentElement.lang = language;
    document.documentElement.dir = direction;
}
