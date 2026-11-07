import { useEffect, useState } from "react"

import { AppSidebar, pages } from "@/components/app-sidebar"
import { SiteHeader } from "@/components/site-header"
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar"
import { TooltipProvider } from "@/components/ui/tooltip"
import { getToken, session } from "@/lib/api"
import { Access } from "@/pages/access"
import { Cash } from "@/pages/cash"
import { Clients } from "@/pages/clients"
import { Company } from "@/pages/company"
import { History } from "@/pages/history"
import { Login } from "@/pages/login"
import { Overview } from "@/pages/overview"

const views: Record<string, () => React.ReactNode> = {
  "visao-geral": Overview,
  historico: History,
  acesso: Access,
  clientes: Clients,
  caixa: Cash,
  empresa: Company,
}

// Hash routing: no plate, document or code ever goes in the URL, only the page id.
const currentPage = () => (location.hash.slice(1) in views ? location.hash.slice(1) : "visao-geral")

export default function App() {
  const [token, setTokenState] = useState(getToken)
  const [page, setPage] = useState(currentPage)
  useEffect(() => {
    const onAuth = () => setTokenState(getToken())
    const onHash = () => setPage(currentPage())
    window.addEventListener("np:auth", onAuth)
    window.addEventListener("hashchange", onHash)
    return () => {
      window.removeEventListener("np:auth", onAuth)
      window.removeEventListener("hashchange", onHash)
    }
  }, [])

  if (!token) return <Login />
  const View = views[page]
  return (
    <TooltipProvider>
      <SidebarProvider style={{ "--sidebar-width": "calc(var(--spacing) * 72)", "--header-height": "calc(var(--spacing) * 12)" } as React.CSSProperties}>
        <AppSidebar variant="inset" current={page} role={session()?.role ?? ""} />
        <SidebarInset>
          <SiteHeader group={pages.find((p) => p.id === page)!.group} title={pages.find((p) => p.id === page)!.title} />
          <main className="@container/main flex flex-1 flex-col gap-4 px-4 py-4 md:gap-6 md:py-6 lg:px-6">
            <View />
          </main>
        </SidebarInset>
      </SidebarProvider>
    </TooltipProvider>
  )
}
