import * as React from "react"
import { BuildingIcon, CarFrontIcon, HistoryIcon, LayoutDashboardIcon, UsersIcon, WalletIcon } from "lucide-react"

import { NavMain } from "@/components/nav-main"
import { NavUser } from "@/components/nav-user"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar"

export const pages = [
  { id: "visao-geral", title: "Visão geral", icon: <LayoutDashboardIcon /> },
  { id: "historico", title: "Histórico", icon: <HistoryIcon /> },
  { id: "acesso", title: "Controle de acesso", icon: <CarFrontIcon /> },
  { id: "clientes", title: "Clientes", icon: <UsersIcon /> },
  { id: "caixa", title: "Fluxo de caixa", icon: <WalletIcon />, roles: ["Owner", "Admin"] },
  { id: "empresa", title: "Empresa", icon: <BuildingIcon /> },
]

export function AppSidebar({ current, role, ...props }: React.ComponentProps<typeof Sidebar> & { current: string; role: string }) {
  return (
    <Sidebar collapsible="offcanvas" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton asChild className="data-[slot=sidebar-menu-button]:p-1.5!">
              <a href="#visao-geral">
                <span className="flex size-6 items-center justify-center rounded bg-primary text-sm font-bold text-primary-foreground">P</span>
                <span className="text-base font-semibold">NeoParking</span>
              </a>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <NavMain current={current} items={pages.filter((p) => !p.roles || p.roles.includes(role))} />
      </SidebarContent>
      <SidebarFooter>
        <NavUser />
      </SidebarFooter>
    </Sidebar>
  )
}
