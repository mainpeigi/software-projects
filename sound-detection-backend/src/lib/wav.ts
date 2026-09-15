export type DecodedWav = {
    sampleRate: number
    channels: number
    /** Mono samples in [-1, 1]; multi-channel input is averaged. */
    samples: Float32Array
}

function chunkId(view: DataView, offset: number): string {
    return String.fromCharCode(view.getUint8(offset), view.getUint8(offset + 1), view.getUint8(offset + 2), view.getUint8(offset + 3))
}

/**
 * Minimal RIFF/WAVE reader for 16/24/32-bit PCM and 32-bit float. It tolerates
 * the "unknown length" data chunks some encoders write when streaming.
 */
export function decodeWav(buffer: ArrayBuffer): DecodedWav {
    const view = new DataView(buffer)
    if (view.byteLength < 12 || chunkId(view, 0) !== "RIFF" || chunkId(view, 8) !== "WAVE") {
        throw new Error("The audio is not a WAV file.")
    }
    let format = 0
    let channels = 0
    let sampleRate = 0
    let bits = 0
    let dataStart = -1
    let dataLength = 0
    let offset = 12
    while (offset + 8 <= view.byteLength) {
        const id = chunkId(view, offset)
        const size = view.getUint32(offset + 4, true)
        const body = offset + 8
        if (id === "fmt " && body + 16 <= view.byteLength) {
            format = view.getUint16(body, true)
            channels = view.getUint16(body + 2, true)
            sampleRate = view.getUint32(body + 4, true)
            bits = view.getUint16(body + 14, true)
            if (format === 0xfffe && size >= 26 && body + 26 <= view.byteLength) format = view.getUint16(body + 24, true)
        } else if (id === "data") {
            dataStart = body
            dataLength = size === 0 || size === 0xffffffff || body + size > view.byteLength ? view.byteLength - body : size
            break
        }
        offset = body + size + (size & 1)
    }
    const bytes = bits / 8
    if (dataStart < 0 || channels < 1 || channels > 8 || sampleRate <= 0 ||
        !((format === 1 && (bits === 16 || bits === 24 || bits === 32)) || (format === 3 && bits === 32))) {
        throw new Error("The WAV audio format is not supported.")
    }
    const frames = Math.floor(dataLength / (bytes * channels))
    const samples = new Float32Array(frames)
    for (let frame = 0; frame < frames; frame += 1) {
        let total = 0
        for (let channel = 0; channel < channels; channel += 1) {
            const position = dataStart + (frame * channels + channel) * bytes
            if (format === 3) total += view.getFloat32(position, true)
            else if (bits === 16) total += view.getInt16(position, true) / 32768
            else if (bits === 24) {
                const value = view.getUint8(position) | (view.getUint8(position + 1) << 8) | (view.getInt8(position + 2) << 16)
                total += value / 8388608
            } else total += view.getInt32(position, true) / 2147483648
        }
        samples[frame] = total / channels
    }
    return { sampleRate, channels, samples }
}
