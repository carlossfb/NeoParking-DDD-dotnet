import { KpiCard, KpiGrid } from "@/components/kpi-card"
import { ErrorAlert, InfoAlert, SpecBadge } from "@/components/notice"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { fmtDate, fmtMoney, useApi } from "@/lib/api"

export type FeeTicket = {
  id: string
  plate: string
  entryAt: string
  exitAt: string | null
  fee: { minimumFee: number; hourlyRate: number; billedHours: number; amount: number; calculatedAt: string } | null
}

export function Cash() {
  const fees = useApi<FeeTicket[]>("/billing/tickets/fee-history")
  const rows = fees.data ?? []
  const total = rows.reduce((s, t) => s + (t.fee?.amount ?? 0), 0)
  const charged = rows.filter((t) => (t.fee?.amount ?? 0) > 0).length

  return (
    <>
      <InfoAlert title="Pagamento ainda não existe (C3)">Os valores são as tarifas calculadas pelo Billing na saída, não caixa recebido.</InfoAlert>
      <KpiGrid>
        <KpiCard title="Receita estimada" value={fmtMoney(total)} loading={fees.loading} foot="Soma das tarifas calculadas" />
        <KpiCard title="Tickets cobrados" value={`${charged} de ${rows.length}`} loading={fees.loading} foot="Os demais saíram na carência" />
        <KpiCard title="Mensalidades" action={<SpecBadge>C4</SpecBadge>} foot="Depende das assinaturas" />
      </KpiGrid>
      <Card>
        <CardHeader>
          <CardTitle>Tarifas calculadas</CardTitle>
          <CardDescription>Tickets finalizados, com a tarifa congelada na saída.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <ErrorAlert error={fees.error} />
          <div className="overflow-hidden rounded-lg border">
            <Table>
              <TableHeader className="bg-muted">
                <TableRow>
                  <TableHead>Placa</TableHead>
                  <TableHead>Saída</TableHead>
                  <TableHead className="text-right">Horas cobradas</TableHead>
                  <TableHead className="text-right">Tarifa mínima</TableHead>
                  <TableHead className="text-right">Valor/hora</TableHead>
                  <TableHead className="text-right">Valor</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell className="font-mono">{t.plate}</TableCell>
                    <TableCell>{fmtDate(t.exitAt)}</TableCell>
                    <TableCell className="text-right tabular-nums">{t.fee?.billedHours ?? "—"}</TableCell>
                    <TableCell className="text-right tabular-nums">{fmtMoney(t.fee?.minimumFee)}</TableCell>
                    <TableCell className="text-right tabular-nums">{fmtMoney(t.fee?.hourlyRate)}</TableCell>
                    <TableCell className="text-right font-medium tabular-nums">{fmtMoney(t.fee?.amount)}</TableCell>
                  </TableRow>
                ))}
                {!fees.loading && rows.length === 0 && (
                  <TableRow><TableCell colSpan={6} className="h-24 text-center text-muted-foreground">Nenhuma tarifa calculada ainda.</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </>
  )
}
