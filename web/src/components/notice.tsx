import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import type { ApiError } from "@/lib/api"

export function SpecBadge({ children = "Spec, não implementado" }: { children?: React.ReactNode }) {
  return <Badge className="h-auto border-amber-300 bg-amber-100 whitespace-normal text-amber-900">{children}</Badge>
}

export function ErrorText({ error }: { error?: ApiError | string }) {
  if (!error) return null
  return <p role="alert" className="text-sm text-destructive">{typeof error === "string" ? error : error.message}</p>
}

export function Missing({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">{title} <SpecBadge /></CardTitle>
        <CardDescription>{children}</CardDescription>
      </CardHeader>
      <CardContent />
    </Card>
  )
}
