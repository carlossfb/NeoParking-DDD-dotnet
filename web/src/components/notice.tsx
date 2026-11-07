import { CircleAlertIcon, ConstructionIcon } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@/components/ui/empty"
import type { ApiError } from "@/lib/api"

export function SpecBadge({ children = "Spec, não implementado" }: { children?: React.ReactNode }) {
  return <Badge className="bg-warning-muted text-warning">{children}</Badge>
}

export function ErrorAlert({ error }: { error?: ApiError | string }) {
  if (!error) return null
  return (
    <Alert variant="destructive">
      <CircleAlertIcon />
      <AlertTitle>{typeof error === "string" ? error : error.message}</AlertTitle>
    </Alert>
  )
}

export function InfoAlert({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Alert>
      <CircleAlertIcon />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>{children}</AlertDescription>
    </Alert>
  )
}

export function Missing({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Empty className="border">
      <EmptyHeader>
        <EmptyMedia variant="icon"><ConstructionIcon /></EmptyMedia>
        <EmptyTitle>{title}</EmptyTitle>
        <EmptyDescription>{children}</EmptyDescription>
        <SpecBadge />
      </EmptyHeader>
    </Empty>
  )
}
