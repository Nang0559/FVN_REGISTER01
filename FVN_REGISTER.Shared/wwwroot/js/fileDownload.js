window.fvnFileDownload = window.fvnFileDownload || {};

window.fvnFileDownload.downloadBase64 = function (fileName, contentType, base64) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);

    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
};

// M? file trong tab m?i n?u trình duy?t xem ???c tr?c ti?p (PDF, PNG, JPEG).
// N?u lo?i file không xem ???c ho?c popup b? ch?n thì t? ??ng chuy?n sang t?i xu?ng.
window.fvnFileDownload.openBase64 = function (fileName, contentType, base64) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);

    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    const type = contentType || "application/octet-stream";
    const blob = new Blob([bytes], { type: type });
    const url = URL.createObjectURL(blob);
    const viewable = type === "application/pdf" || type === "image/png" || type === "image/jpeg";

    if (viewable) {
        const opened = window.open(url, "_blank");
        if (opened) {
            setTimeout(function () { URL.revokeObjectURL(url); }, 120000);
            return;
        }
    }

    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    anchor.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
};
