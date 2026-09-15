export class ApiError extends Error {
  constructor(public readonly status: number, message: string) { super(message) }
}

export function requireId(value: string): string {
  if (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value)) throw new ApiError(400, "Invalid identifier.")
  return value.toLowerCase()
}
