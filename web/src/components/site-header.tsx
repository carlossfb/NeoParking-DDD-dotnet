import { Separator } from "@/components/ui/separator"
import { SidebarTrigger } from "@/components/ui/sidebar"

export function SiteHeader({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <header className="flex h-(--header-height) shrink-0 items-center gap-2 border-b bg-background">
      <div className="flex w-full items-center gap-1 px-4 lg:gap-2 lg:px-6">
        <SidebarTrigger className="-ml-1 size-11" aria-label="Abrir menu" />
        <Separator orientation="vertical" className="mx-2 data-[orientation=vertical]:h-4" />
        <h1 className="text-base font-medium">{title}</h1>
        <div className="ml-auto flex items-center gap-2">{children}</div>
      </div>
    </header>
  )
}
