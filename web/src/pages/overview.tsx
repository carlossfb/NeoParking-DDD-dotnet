import { useState } from "react"
import { SearchIcon } from "lucide-react"
import { Bar, BarChart, CartesianGrid, Cell, XAxis, YAxis } from "recharts"

import { ErrorAlert, SpecBadge } from "@/components/notice"
import { Plate } from "@/components/plate"
import { Badge } from "@/components/ui/badge"
import { Card, CardAction, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from "@/components/ui/chart"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group"
import {
  apiTime, fmtDate, fmtDuration, fmtMoney, hourOf, minutesBetween, session, startOfDay, useApi, type Paged,
} from "@/lib/api"
import type { Ticket } from "@/pages/access"
import type { FeeTicket } from "@/pages/cash"

const chartConfig = { entradas: { label: "Entradas", color: "var(--chart-1)" } } satisfies ChartConfig
const exitLabel: Record<string, string> = { Code: "pelo código", Plate: "pela placa" }
const LONG_STAY_MIN = 120

export function Overview() {
  const [range, setRange] = useState("hoje")
  const [query, setQuery] = useState("")
  const canSeeMoney = ["Owner", "Admin"].includes(session()?.role ?? "")
  const today = startOfDay(0)
  // Fixed at mount: a time computed on every render changes the URL and refetches in a loop.
  const [sameTimeYesterday] = useState(() => apiTime(new Date(Date.now() - 24 * 60 * 60 * 1000)))

  // shortcut: each list reads at most 100 tickets (the API's page cap); swap for an aggregation route when there is one.
  const open = useApi<Paged<Ticket>>("/access/tickets?status=Open&pageSize=100")
  const todayTickets = useApi<Paged<Ticket>>(`/access/tickets?entryFrom=${today}&pageSize=100`)
  const yesterday = useApi<Paged<Ticket>>(`/access/tickets?entryFrom=${startOfDay(1)}&entryTo=${sameTimeYesterday}&pageSize=1`)
  const week = useApi<Paged<Ticket>>(range === "7d" ? `/access/tickets?entryFrom=${startOfDay(6)}&pageSize=100` : null)
  const closed = useApi<Paged<Ticket>>("/access/tickets?status=Closed&pageSize=5")
  const fees = useApi<FeeTicket[]>(canSeeMoney ? `/billing/tickets/fee-history?from=${today}` : null)

  const source = (range === "7d" ? week.data?.items : todayTickets.data?.items) ?? []
  const perHour = Array.from({ length: 17 }, (_, i) => ({
    hora: `${String(6 + i).padStart(2, "0")}h`,
    entradas: source.filter((t) => hourOf(t.entryAt) === 6 + i).length,
  }))
  const peak = perHour.reduce((best, h, i) => (h.entradas > perHour[best].entradas ? i : best), 0)
  const delta = todayTickets.data && yesterday.data ? todayTickets.data.totalCount - yesterday.data.totalCount : undefined
  const revenue = fees.data?.reduce((sum, t) => sum + (t.fee?.amount ?? 0), 0)
  const rows = (open.data?.items ?? []).filter((t) => t.plate.includes(query.toUpperCase().replace(/[^A-Z0-9]/g, "")))

  return (
    <>
      <ErrorAlert error={open.error ?? todayTickets.error ?? fees.error} />

      <section aria-label="Resumo do dia" className="grid gap-4 @xl/main:grid-cols-2 @5xl/main:grid-cols-[1.4fr_1fr_1fr_1fr]">
        <Card>
          <CardHeader>
            <CardDescription>No pátio agora</CardDescription>
            <CardTitle className="text-3xl font-semibold tabular-nums">
              {open.loading ? <Skeleton className="h-8 w-16" /> : open.data?.totalCount}{" "}
              <span className="text-sm font-normal text-muted-foreground">veículos</span>
            </CardTitle>
            <CardAction><SpecBadge>Capacidade: C1</SpecBadge></CardAction>
          </CardHeader>
          <CardFooter className="text-sm text-muted-foreground">
            {perHour[peak].entradas > 0 && range === "hoje" ? `Pico de entradas hoje: ${perHour[peak].entradas} às ${perHour[peak].hora}` : "Sem entradas hoje ainda"}
          </CardFooter>
        </Card>
        <Card>
          <CardHeader>
            <CardDescription>Entradas hoje</CardDescription>
            <CardTitle className="text-3xl font-semibold tabular-nums">
              {todayTickets.loading ? <Skeleton className="h-8 w-12" /> : todayTickets.data?.totalCount}
            </CardTitle>
          </CardHeader>
          <CardFooter>
            {delta !== undefined && (
              <Badge className={delta >= 0 ? "bg-success-muted text-success" : "bg-warning-muted text-warning"}>
                {delta >= 0 ? "+" : ""}{delta} vs ontem neste horário
              </Badge>
            )}
          </CardFooter>
        </Card>
        <Card>
          <CardHeader>
            <CardDescription>Saídas hoje</CardDescription>
            <CardTitle className="text-3xl font-semibold">—</CardTitle>
            <CardAction><SpecBadge>Rota nova</SpecBadge></CardAction>
          </CardHeader>
          <CardFooter className="text-sm text-muted-foreground">A lista não filtra por data de saída</CardFooter>
        </Card>
        <Card>
          <CardHeader>
            <CardDescription>Receita estimada</CardDescription>
            <CardTitle className="text-3xl font-semibold tabular-nums">
              {!canSeeMoney ? "—" : fees.loading ? <Skeleton className="h-8 w-24" /> : fmtMoney(revenue)}
            </CardTitle>
          </CardHeader>
          <CardFooter className="text-sm text-muted-foreground">
            {canSeeMoney ? "Tarifas calculadas, ainda não no caixa" : "Visível para Owner e Admin"}
          </CardFooter>
        </Card>
      </section>

      <section className="grid gap-4 @4xl/main:grid-cols-[1.5fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Entradas por hora</CardTitle>
            <CardDescription>
              06h às 22h · {source.length} veículos{range === "7d" ? " em 7 dias" : ""}
            </CardDescription>
            <CardAction>
              <ToggleGroup type="single" variant="outline" size="sm" value={range} onValueChange={(v) => v && setRange(v)}>
                <ToggleGroupItem value="hoje">Hoje</ToggleGroupItem>
                <ToggleGroupItem value="7d">7 dias</ToggleGroupItem>
              </ToggleGroup>
            </CardAction>
          </CardHeader>
          <CardContent className="px-2 sm:px-6">
            <ChartContainer config={chartConfig} className="aspect-auto h-56 w-full">
              <BarChart data={perHour}>
                <CartesianGrid vertical={false} />
                <XAxis dataKey="hora" tickLine={false} axisLine={false} tickMargin={8} interval={1} className="font-mono" />
                <YAxis allowDecimals={false} tickLine={false} axisLine={false} width={28} />
                <ChartTooltip cursor={false} content={<ChartTooltipContent indicator="dot" />} />
                <Bar dataKey="entradas" radius={3} isAnimationActive={false} minPointSize={2}>
                  {perHour.map((h, i) => (
                    <Cell key={h.hora} fill={h.entradas === 0 ? "var(--chart-4)" : i === peak ? "var(--primary)" : "var(--color-entradas)"} />
                  ))}
                </Bar>
              </BarChart>
            </ChartContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Finalizados recentes</CardTitle>
            <CardDescription>Últimos tickets com saída registrada</CardDescription>
          </CardHeader>
          <CardContent className="grid">
            {closed.data?.items.map((t) => (
              <div key={t.id} className="flex items-center gap-3 border-t py-2.5 first:border-t-0">
                <Plate value={t.plate} />
                <div className="grid flex-1 text-sm leading-tight">
                  <span>{fmtDate(t.exitAt)}</span>
                  <span className="text-xs text-muted-foreground">{fmtDuration(t.entryAt, t.exitAt)} no pátio</span>
                </div>
                <Badge variant="secondary">{t.exitMethod ? exitLabel[t.exitMethod] ?? t.exitMethod : "—"}</Badge>
              </div>
            ))}
            {closed.data?.items.length === 0 && <p className="text-sm text-muted-foreground">Nenhuma saída ainda.</p>}
          </CardContent>
        </Card>
      </section>

      <Card className="gap-0 pb-0">
        <CardHeader className="pb-4">
          <CardTitle>No pátio agora</CardTitle>
          <CardDescription>{open.data ? `${open.data.totalCount} tickets abertos` : "Carregando…"}</CardDescription>
          <CardAction>
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Buscar placa" aria-label="Buscar placa" className="w-44 bg-background pl-8" />
            </div>
          </CardAction>
        </CardHeader>
        <div className="overflow-x-auto">
          <Table>
            <TableHeader className="bg-muted">
              <TableRow>
                <TableHead className="pl-6">Placa</TableHead>
                <TableHead>Código</TableHead>
                <TableHead>Cliente</TableHead>
                <TableHead>Entrada</TableHead>
                <TableHead>Permanência</TableHead>
                <TableHead className="pr-6">Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((t) => {
                const long = minutesBetween(t.entryAt) > LONG_STAY_MIN
                return (
                  <TableRow key={t.id}>
                    <TableCell className="pl-6"><Plate value={t.plate} /></TableCell>
                    <TableCell className="font-mono text-xs text-muted-foreground">{t.code.slice(0, 8)}…</TableCell>
                    <TableCell>{t.clientId ? "Cadastrado" : "Avulso"}</TableCell>
                    <TableCell>{fmtDate(t.entryAt)}</TableCell>
                    <TableCell className="tabular-nums">{fmtDuration(t.entryAt)}</TableCell>
                    <TableCell className="pr-6">
                      <Badge className={long ? "bg-warning-muted text-warning" : "bg-success-muted text-success"}>
                        {long ? "Acima de 2h" : "No pátio"}
                      </Badge>
                    </TableCell>
                  </TableRow>
                )
              })}
              {open.data && rows.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="h-20 text-center text-muted-foreground">
                    {query ? "Nenhuma placa encontrada." : "Nenhum veículo no pátio agora."}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </div>
      </Card>
    </>
  )
}
