import { useState } from "react"
import { SquareParkingIcon } from "lucide-react"

import { LoginForm } from "@/components/login-form"
import { api, setToken } from "@/lib/api"

export function Login() {
  const [error, setError] = useState<string>()
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    setBusy(true)
    setError(undefined)
    try {
      const { token } = await api<{ token: string }>("/operators/login", "POST", {
        email: form.get("email"),
        password: form.get("password"),
      })
      setToken(token)
    } catch {
      // The API gives the same 401 for unknown e-mail, wrong password and inactive account.
      setError("E-mail ou senha incorretos.")
      setBusy(false)
    }
  }

  return (
    <div className="grid min-h-svh lg:grid-cols-2">
      <div className="flex flex-col gap-4 p-6 md:p-10">
        <div className="flex justify-center gap-2 md:justify-start">
          <span className="flex items-center gap-2 font-medium">
            <span className="flex size-6 items-center justify-center rounded-md bg-primary text-primary-foreground">
              <SquareParkingIcon className="size-4" />
            </span>
            NeoParking
          </span>
        </div>
        <div className="flex flex-1 items-center justify-center">
          <div className="w-full max-w-xs">
            <LoginForm onSubmit={submit} error={error} busy={busy} />
          </div>
        </div>
      </div>
      {/* login-02 shows a cover image here; the brand panel uses the sidebar tokens instead of a stock photo. */}
      <div className="relative hidden flex-col justify-end gap-2 bg-sidebar p-10 text-sidebar-foreground lg:flex">
        <SquareParkingIcon className="mb-auto size-10 text-sidebar-primary" />
        <p className="text-2xl font-semibold text-sidebar-accent-foreground">Entradas, saídas e cobrança num só lugar.</p>
        <p className="text-sm">Controle de acesso, clientes e tarifas do estacionamento.</p>
      </div>
    </div>
  )
}
