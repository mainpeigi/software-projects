import { describe, expect, it } from "vitest"

import { decodeWav } from "./wav"
import { encodeWav } from "@/test/synthetic-audio"

function wav({ format = 1, channels = 1, bits = 16, sampleRate = 48_000, data, dataSize }: {
  format?: number; channels?: number; bits?: number; sampleRate?: number; data: ArrayBuffer; dataSize?: number
}): ArrayBuffer {
  const buffer = new ArrayBuffer(44 + data.byteLength)
  const view = new DataView(buffer)
  const text = (offset: number, value: string) => [...value].forEach((char, index) => view.setUint8(offset + index, char.charCodeAt(0)))
  text(0, "RIFF"); view.setUint32(4, 36 + data.byteLength, true); text(8, "WAVE")
  text(12, "fmt "); view.setUint32(16, 16, true); view.setUint16(20, format, true); view.setUint16(22, channels, true)
  view.setUint32(24, sampleRate, true); view.setUint32(28, sampleRate * channels * bits / 8, true)
  view.setUint16(32, channels * bits / 8, true); view.setUint16(34, bits, true)
  text(36, "data"); view.setUint32(40, dataSize ?? data.byteLength, true)
  new Uint8Array(buffer, 44).set(new Uint8Array(data))
  return buffer
}

describe("decodeWav", () => {
  it("reads 16-bit mono PCM", () => {
    const decoded = decodeWav(encodeWav(Float32Array.of(0, 0.5, -0.5, 1)))
    expect(decoded.sampleRate).toBe(48_000)
    expect(decoded.channels).toBe(1)
    expect([...decoded.samples].map((value) => Number(value.toFixed(3)))).toEqual([0, 0.5, -0.5, 1])
  })

  it("averages stereo channels into mono", () => {
    const data = new Int16Array([16384, 0, -16384, -16384])
    const decoded = decodeWav(wav({ channels: 2, data: data.buffer }))
    expect([...decoded.samples]).toEqual([0.25, -0.5])
  })

  it("reads 32-bit float and 24-bit PCM", () => {
    expect([...decodeWav(wav({ format: 3, bits: 32, data: Float32Array.of(0.25, -0.75).buffer })).samples]).toEqual([0.25, -0.75])
    const pcm24 = new Uint8Array([0x00, 0x00, 0x40, 0x00, 0x00, 0xc0])
    expect([...decodeWav(wav({ bits: 24, data: pcm24.buffer })).samples]).toEqual([0.5, -0.5])
  })

  it("accepts a streamed data chunk of unknown length", () => {
    const decoded = decodeWav(wav({ data: new Int16Array([8192, 8192, 8192]).buffer, dataSize: 0xffffffff }))
    expect(decoded.samples).toHaveLength(3)
  })

  it("rejects other files and unsupported encodings", () => {
    expect(() => decodeWav(new ArrayBuffer(20))).toThrow("not a WAV")
    expect(() => decodeWav(wav({ format: 2, data: new ArrayBuffer(4) }))).toThrow("not supported")
  })
})
