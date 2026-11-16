window.downloadFileFromByteArray = function (fileName, contentType, byteArray) {
    const blob = new Blob([new Uint8Array(byteArray)], { type: contentType });
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? 'relatorio.bin';
    anchorElement.click();
    anchorElement.remove();
    URL.revokeObjectURL(url);
};
