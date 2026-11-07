import { LogOutIcon } from "lucide-react"

import { Button } from "@/components/ui/button"
import { session, setToken, useApi } from "@/lib/api"

export function NavUser() {
  const me = useApi<{ name: string; email: string; role: string }>("/operators/me")
  const role = session()?.role
  return (
    <div className="flex items-center gap-2 border-t pt-3">
      <div className="grid flex-1 text-sm leading-tight">
        <span className="truncate font-medium">{me.data?.name ?? "…"}</span>
        <span className="truncate text-xs text-muted-foreground">{role}</span>
      </div>
      <Button variant="ghost" size="icon" className="size-11" aria-label="Sair" onClick={() => setToken(null)}>
        <LogOutIcon />
      </Button>
    </div>
  )
}
