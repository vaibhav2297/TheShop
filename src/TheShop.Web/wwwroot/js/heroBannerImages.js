function contentType(bytes) {
    if (bytes.length >= 8 && [137, 80, 78, 71, 13, 10, 26, 10].every((b, i) => bytes[i] === b))
        return "image/png";
    if (bytes.length >= 3 && bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255)
        return "image/jpeg";
    if (bytes.length >= 12 && String.fromCharCode(...bytes.slice(0, 4)) === "RIFF"
        && String.fromCharCode(...bytes.slice(8, 12)) === "WEBP") return "image/webp";
    return null;
}

export async function inspect(bytes) {
    const type = contentType(bytes);
    if (!type) return { contentType: null, width: 0, height: 0, isDecodable: false };
    const url = URL.createObjectURL(new Blob([bytes], { type }));
    try {
        const image = new Image();
        image.src = url;
        await image.decode();
        return { contentType: type, width: image.naturalWidth, height: image.naturalHeight, isDecodable: true };
    } catch {
        return { contentType: type, width: 0, height: 0, isDecodable: false };
    } finally {
        URL.revokeObjectURL(url);
    }
}

