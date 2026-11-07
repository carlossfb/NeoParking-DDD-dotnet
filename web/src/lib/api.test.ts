import assert from "node:assert/strict"
import { test } from "node:test"

const store = new Map<string, string>()
globalThis.sessionStorage = {
  getItem: (k: string) => store.get(k) ?? null,
  setItem: (k: string, v: string) => void store.set(k, v),
  removeItem: (k: string) => void store.delete(k),
} as Storage
globalThis.window = new EventTarget() as Window & typeof globalThis

const { api, ApiError, fmtDuration, hourOf, setToken } = await import("./api.ts")

test("API times without a zone are read as UTC", () => {
  assert.equal(hourOf("2026-11-07T17:22:03.66"), new Date(Date.UTC(2026, 10, 7, 17, 22, 3)).getHours())
  assert.equal(fmtDuration("2026-11-07T10:00:00", "2026-11-07T11:05:00"), "1h 05min")
  assert.equal(fmtDuration("2026-11-07T10:00:00", "2026-11-07T10:17:00"), "17min")
})

test("known API messages are translated and a 401 ends the session", async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({ status: 422, error: "Invalid plate format." }), { status: 422 })
  await assert.rejects(api("/access/tickets", "POST", { plate: "X" }), (e: unknown) =>
    e instanceof ApiError && e.status === 422 && e.message.startsWith("Placa inválida"))

  setToken("a.b.c")
  globalThis.fetch = async () => new Response("", { status: 401 })
  await assert.rejects(api("/clients"), (e: unknown) => e instanceof ApiError && e.status === 401)
  assert.equal(sessionStorage.getItem("np.token"), null)
})
