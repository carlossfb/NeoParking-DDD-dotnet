import { useState } from "react"

import { ErrorAlert } from "@/components/notice"
import { Plate } from "@/components/plate"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Skeleton } from "@/components/ui/skeleton"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { api, ApiError, fmtDate, fmtDuration, useApi, type Paged } from "@/lib/api"

export type Ticket = {
  id: string
  code: string
  plate: string
  clientId: string | null
  entryAt: string
  exitAt: string | null
  status: "Open" | "Closed"
  exitMethod: string | null
}

const exitLabel: Record<string, string> = { Code: "Código", Plate: "Placa + motivo" }

export function Access() {
  const [status, setStatus] = useState("Open")
  const [page, setPage] = useState(1)
  const query = `/access/tickets?page=${page}&pageSize=20${status ? `&status=${status}` : ""}`
  const list = useApi<Paged<Ticket>>(query)

  return (
    <>
      <div className="grid gap-4 lg:grid-cols-2">
        <QuickForm
          title="Registrar entrada"
          description="Digite a placa do veículo que está entrando."
          label="Placa"
          name="plate"
          action="Registrar entrada"
          submit={(plate) => api<Ticket>("/access/tickets", "POST", { plate: plate.toUpperCase() })}
          done={(t) => `Entrada registrada: ${t.plate}, código ${t.code}.`}
          onDone={list.reload}
        />
        <QuickForm
          title="Registrar saída"
          description="Leia o QR do ticket ou digite o código de 20 caracteres."
          label="Código do ticket"
          name="code"
          action="Registrar saída"
          mono
          // The code is random hex, never personal data, so it may go in the path.
          submit={(code) => api<Ticket>(`/access/tickets/by-code/${encodeURIComponent(code.trim().toUpperCase())}/exit`, "POST", {})}
          done={(t) => `Saída registrada: ${t.plate}, ${fmtDuration(t.entryAt, t.exitAt)} no pátio.`}
          onDone={list.reload}
        />
      </div>

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
          <div>
            <CardTitle>Tickets</CardTitle>
            <CardDescription>{list.data ? `${list.data.totalCount} no total` : "Carregando…"}</CardDescription>
          </div>
          <Tabs value={status} onValueChange={(v) => { setStatus(v); setPage(1) }}>
            <TabsList>
              <TabsTrigger value="Open">Abertos</TabsTrigger>
              <TabsTrigger value="Closed">Finalizados</TabsTrigger>
              <TabsTrigger value="">Todos</TabsTrigger>
            </TabsList>
          </Tabs>
        </CardHeader>
        <CardContent className="grid gap-3">
          <ErrorAlert error={list.error} />
          <div className="overflow-hidden rounded-lg border">
            <Table>
              <TableHeader className="bg-muted">
                <TableRow>
                  <TableHead>Placa</TableHead>
                  <TableHead>Código</TableHead>
                  <TableHead>Entrada</TableHead>
                  <TableHead>Saída</TableHead>
                  <TableHead>Permanência</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Saída por</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {list.loading && !list.data
                  ? [0, 1, 2].map((i) => (
                      <TableRow key={i}>
                        <TableCell colSpan={7}><Skeleton className="h-5 w-full" /></TableCell>
                      </TableRow>
                    ))
                  : list.data?.items.map((t) => (
                      <TableRow key={t.id}>
                        <TableCell><Plate value={t.plate} /></TableCell>
                        <TableCell className="font-mono text-xs">{t.code}</TableCell>
                        <TableCell>{fmtDate(t.entryAt)}</TableCell>
                        <TableCell>{fmtDate(t.exitAt)}</TableCell>
                        <TableCell>{fmtDuration(t.entryAt, t.exitAt)}</TableCell>
                        <TableCell>
                          <Badge variant={t.status === "Open" ? "default" : "secondary"} className={t.status === "Open" ? "bg-success-muted text-success" : undefined}>{t.status === "Open" ? "Aberto" : "Finalizado"}</Badge>
                        </TableCell>
                        <TableCell>{t.exitMethod ? exitLabel[t.exitMethod] ?? t.exitMethod : "—"}</TableCell>
                      </TableRow>
                    ))}
                {list.data?.items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} className="h-24 text-center text-muted-foreground">
                      {status === "Open" ? "Nenhum veículo no pátio agora." : "Nenhum ticket aqui ainda."}
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
          {list.data && list.data.totalPages > 1 && (
            <div className="flex items-center justify-end gap-2 text-sm text-muted-foreground">
              Página {list.data.page} de {list.data.totalPages}
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Anterior</Button>
              <Button variant="outline" size="sm" disabled={page >= list.data.totalPages} onClick={() => setPage(page + 1)}>Próxima</Button>
            </div>
          )}
        </CardContent>
      </Card>
    </>
  )
}

function QuickForm<T>(props: {
  title: string
  description: string
  label: string
  name: string
  action: string
  mono?: boolean
  submit: (value: string) => Promise<T>
  done: (result: T) => string
  onDone: () => void
}) {
  const [msg, setMsg] = useState<{ ok: boolean; text: string }>()
  const [busy, setBusy] = useState(false)

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = e.currentTarget
    const value = String(new FormData(form).get(props.name) ?? "").trim()
    setBusy(true)
    try {
      const result = await props.submit(value)
      setMsg({ ok: true, text: props.done(result) })
      form.reset()
      props.onDone()
    } catch (err) {
      // Keep what was typed so the operator can fix it.
      setMsg({ ok: false, text: err instanceof ApiError ? err.message : "Erro inesperado. Tente de novo." })
    } finally {
      setBusy(false)
      form.querySelector("input")?.focus()
    }
  }

  const id = `f-${props.name}`
  return (
    <Card>
      <CardHeader>
        <CardTitle>{props.title}</CardTitle>
        <CardDescription>{props.description}</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit}>
          <Field data-invalid={msg && !msg.ok}>
            <FieldLabel htmlFor={id}>{props.label}</FieldLabel>
            <div className="flex gap-2">
              <Input
                id={id}
                name={props.name}
                required
                autoComplete="off"
                aria-invalid={msg && !msg.ok}
                className={`h-11 ${props.mono ? "font-mono" : ""}`}
              />
              <Button type="submit" className="h-11" disabled={busy}>
                {busy && <Spinner />}
                {props.action}
              </Button>
            </div>
            {msg?.ok === false && <FieldError>{msg.text}</FieldError>}
            {msg?.ok && <FieldDescription role="status">{msg.text}</FieldDescription>}
          </Field>
        </form>
      </CardContent>
    </Card>
  )
}
