import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts"

import { ErrorText, SpecBadge } from "@/components/notice"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from "@/components/ui/chart"
import { Skeleton } from "@/components/ui/skeleton"
import { fmtMoney, hourOf, session, startOfToday, useApi, type Paged } from "@/lib/api"
import type { Ticket } from "@/pages/access"
import type { FeeTicket } from "@/pages/cash"

const chartConfig = { entradas: { label: "Entradas", color: "var(--chart-1)" } } satisfies ChartConfig

export function Overview() {
  const today = startOfToday()
  const canSeeMoney = ["Owner", "Admin"].includes(session()?.role ?? "")
  const open = useApi<Paged<Ticket>>("/access/tickets?status=Open&pageSize=1")
  // shortcut: reads at most 100 of today's tickets (the API's page cap); swap for an aggregation route when there is one.
  const todayTickets = useApi<Paged<Ticket>>(`/access/tickets?entryFrom=${today}&pageSize=100`)
  const fees = useApi<FeeTicket[]>(canSeeMoney ? `/billing/tickets/fee-history?from=${today}` : null)

  const items = todayTickets.data?.items ?? []
  const firstHour = 6
  const perHour = Array.from({ length: 17 }, (_, i) => ({
    hora: `${String(firstHour + i).padStart(2, "0")}h`,
    entradas: items.filter((t) => hourOf(t.entryAt) === firstHour + i).length,
  }))
  const revenue = fees.data?.reduce((sum, t) => sum + (t.fee?.amount ?? 0), 0)

  return (
    <>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi title="Tickets abertos agora" value={open.data?.totalCount} loading={open.loading} foot="Veículos no pátio" />
        <Kpi title="Entradas hoje" value={todayTickets.data?.totalCount} loading={todayTickets.loading} foot="Desde 00h de hoje" />
        <Kpi
          title="Saídas hoje"
          foot={<SpecBadge>Rota nova necessária: a lista não filtra por data de saída</SpecBadge>}
        />
        {canSeeMoney ? (
          <Kpi title="Receita estimada hoje" value={revenue === undefined ? undefined : fmtMoney(revenue)} loading={fees.loading} foot="Tarifas calculadas, não caixa recebido" />
        ) : (
          <Kpi title="Receita estimada hoje" foot="Visível para Owner e Admin" />
        )}
      </div>
      <ErrorText error={open.error ?? todayTickets.error ?? fees.error} />

      <Card>
        <CardHeader>
          <CardTitle>Entradas por hora</CardTitle>
          <CardDescription className="flex flex-wrap items-center gap-2">
            Hoje, das 06h às 22h
            {todayTickets.data && todayTickets.data.totalCount > 100 && <SpecBadge>Mostrando 100 de {todayTickets.data.totalCount}: falta rota de agregação</SpecBadge>}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ChartContainer config={chartConfig} className="aspect-auto h-64 w-full">
            <BarChart data={perHour}>
              <CartesianGrid vertical={false} />
              <XAxis dataKey="hora" tickLine={false} axisLine={false} />
              <YAxis allowDecimals={false} tickLine={false} axisLine={false} width={28} />
              <ChartTooltip content={<ChartTooltipContent />} />
              <Bar dataKey="entradas" fill="var(--color-entradas)" radius={2} isAnimationActive={false} />
            </BarChart>
          </ChartContainer>
        </CardContent>
      </Card>
    </>
  )
}

function Kpi({ title, value, loading, foot }: { title: string; value?: React.ReactNode; loading?: boolean; foot: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardDescription>{title}</CardDescription>
        <CardTitle className="text-3xl tabular-nums">
          {loading ? <Skeleton className="h-9 w-20" /> : (value ?? "—")}
        </CardTitle>
      </CardHeader>
      <CardFooter className="text-sm text-muted-foreground">{foot}</CardFooter>
    </Card>
  )
}
