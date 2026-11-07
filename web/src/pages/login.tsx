import { useState } from "react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
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
    <main className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-xl">NeoParking</CardTitle>
          <CardDescription>Entre com sua conta de operador.</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={submit} className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="email">E-mail</Label>
              <Input id="email" name="email" type="email" autoComplete="username" required className="h-11" />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="password">Senha</Label>
              <Input id="password" name="password" type="password" autoComplete="current-password" required className="h-11" />
            </div>
            {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
            <Button type="submit" className="h-11" disabled={busy}>Entrar</Button>
          </form>
        </CardContent>
      </Card>
    </main>
  )
}
