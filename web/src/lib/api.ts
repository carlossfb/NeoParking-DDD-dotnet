import { useEffect, useReducer, useState } from "react"

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

export type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }

// sessionStorage: the token dies with the tab, so a shared gatehouse PC doesn't keep a session.
const KEY = "np.token"
export const getToken = () => sessionStorage.getItem(KEY)
export const setToken = (token: string | null) => {
  if (token) sessionStorage.setItem(KEY, token)
  else sessionStorage.removeItem(KEY)
  window.dispatchEvent(new Event("np:auth"))
}

// The API answers in English; the UI translates by status and by the known messages.
const known: Record<string, string> = {
  "Invalid plate format.": "Placa inválida. Use 3 letras e 4 caracteres, como TST1B23 ou TST1234.",
  "This vehicle already has an open ticket.": "Essa placa já tem um ticket aberto. Registre a saída dele antes.",
  "Ticket is already closed.": "Esse ticket já foi finalizado.",
  "No ticket was found for this code.": "Nenhum ticket com esse código. Leia o QR de novo ou busque pela placa.",
  "No open ticket was found for this plate.": "Nenhum ticket aberto para essa placa.",
  "The operation conflicts with existing data.": "Já existe um cadastro com essa placa ou esse documento.",
  "The data was changed by another operation.": "Os dados mudaram enquanto você editava. Recarregue e tente de novo.",
}
const byStatus: Record<number, string> = {
  401: "Sua sessão expirou. Entre de novo.",
  403: "Você não tem permissão para isso. Fale com um Owner ou Admin.",
  404: "Não encontrado.",
}

export async function api<T>(path: string, method = "GET", body?: unknown): Promise<T> {
  const token = getToken()
  const res = await fetch("/api" + path, {
    method,
    headers: { "Content-Type": "application/json", ...(token && { Authorization: `Bearer ${token}` }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await res.text()
  const data = text ? JSON.parse(text) : null
  if (res.ok) return data as T
  if (res.status === 401 && token && path !== "/operators/login") setToken(null)
  const error: string | undefined = data?.error
  throw new ApiError(res.status, (error && known[error]) ?? byStatus[res.status] ?? error ?? "Erro inesperado. Tente de novo.")
}

export function useApi<T>(path: string | null) {
  const [state, setState] = useState<{ data?: T; error?: ApiError; loading: boolean }>({ loading: !!path })
  const [version, reload] = useReducer((n: number) => n + 1, 0)
  useEffect(() => {
    if (!path) return
    let live = true
    setState((s) => ({ ...s, loading: true }))
    api<T>(path).then(
      (data) => live && setState({ data, loading: false }),
      (error) => live && setState({ error, loading: false }),
    )
    return () => {
      live = false
    }
  }, [path, version])
  return { ...state, reload }
}

export function session() {
  const token = getToken()
  if (!token) return null
  const claims = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")))
  return {
    email: claims.email as string,
    role: (claims.role ?? claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]) as string,
  }
}

// The API stores and returns UTC without a zone suffix ("2026-11-07T17:22:03"); read it as UTC.
const utc = (iso: string) => new Date(/[zZ]|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + "Z")
const dateTime = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" })
const money = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" })
export const fmtDate = (iso?: string | null) => (iso ? dateTime.format(utc(iso)) : "—")
export const fmtMoney = (n?: number | null) => (n == null ? "—" : money.format(n))
export function fmtDuration(from: string, to?: string | null) {
  const min = Math.max(0, Math.round(((to ? utc(to) : new Date()).getTime() - utc(from).getTime()) / 60000))
  return min < 60 ? `${min}min` : `${Math.floor(min / 60)}h ${String(min % 60).padStart(2, "0")}min`
}
export const hourOf = (iso: string) => utc(iso).getHours()
export const startOfToday = () => {
  const d = new Date()
  d.setHours(0, 0, 0, 0)
  return d.toISOString().slice(0, -1) // UTC wall time without "Z", the same form the API stores
}
