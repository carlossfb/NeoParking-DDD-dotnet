import * as React from "react"
import { BuildingIcon, CarFrontIcon, HistoryIcon, LayoutDashboardIcon, UsersIcon, WalletIcon } from "lucide-react"

import { NavUser } from "@/components/nav-user"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarSeparator,
} from "@/components/ui/sidebar"
import { useApi, type Paged } from "@/lib/api"

export const pages = [
  { id: "visao-geral", title: "Visão geral", group: "Operação", icon: <LayoutDashboardIcon /> },
  { id: "acesso", title: "Controle de acesso", group: "Operação", icon: <CarFrontIcon /> },
  { id: "historico", title: "Histórico", group: "Operação", icon: <HistoryIcon /> },
  { id: "clientes", title: "Clientes", group: "Gestão", icon: <UsersIcon /> },
  { id: "caixa", title: "Fluxo de caixa", group: "Gestão", icon: <WalletIcon />, roles: ["Owner", "Admin"] },
  { id: "empresa", title: "Empresa", group: "Gestão", icon: <BuildingIcon /> },
]

export function AppSidebar({ current, role, ...props }: React.ComponentProps<typeof Sidebar> & { current: string; role: string }) {
  const open = useApi<Paged<unknown>>("/access/tickets?status=Open&pageSize=1")
  const visible = pages.filter((p) => !p.roles || p.roles.includes(role))
  return (
    <Sidebar collapsible="offcanvas" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton asChild size="lg">
              <a href="#visao-geral">
                <span className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary font-semibold text-sidebar-primary-foreground">P</span>
                <span className="grid flex-1 text-left leading-tight">
                  <span className="truncate font-semibold text-sidebar-accent-foreground">NeoParking</span>
                  <span className="truncate text-xs text-sidebar-muted">Painel de operação</span>
                </span>
              </a>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarSeparator className="mx-0" />
      <SidebarContent>
        {["Operação", "Gestão"].map((group) => (
          <SidebarGroup key={group}>
            <SidebarGroupLabel className="uppercase tracking-wider text-sidebar-muted">{group}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {visible.filter((p) => p.group === group).map((p) => (
                  <SidebarMenuItem key={p.id}>
                    <SidebarMenuButton
                      asChild
                      isActive={p.id === current}
                      tooltip={p.title}
                      className="data-active:shadow-[inset_3px_0_0_var(--sidebar-primary)]"
                    >
                      <a href={`#${p.id}`} aria-current={p.id === current ? "page" : undefined}>
                        {p.icon}
                        <span>{p.title}</span>
                      </a>
                    </SidebarMenuButton>
                    {p.id === "acesso" && !!open.data?.totalCount && (
                      <SidebarMenuBadge className="bg-sidebar-border font-mono">{open.data.totalCount}</SidebarMenuBadge>
                    )}
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>
      <SidebarSeparator className="mx-0" />
      <SidebarFooter>
        <NavUser />
      </SidebarFooter>
    </Sidebar>
  )
}
