import { ErrorAlert, Missing } from "@/components/notice"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { fmtDate, fmtMoney, useApi } from "@/lib/api"

type Policy = {
  minimumFee: number
  hourlyRate: number
  minimumFeeCoverageMinutes: number
  gracePeriodMinutes: number
  effectiveFrom: string
}

export function Company() {
  const policy = useApi<Policy>("/billing/pricing-policies/active")
  const p = policy.data
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">Tarifa vigente {p && <Badge variant="secondary">Ativa</Badge>}</CardTitle>
          <CardDescription>Versionada: uma nova tarifa encerra a atual e o histórico fica guardado.</CardDescription>
        </CardHeader>
        <CardContent>
          <ErrorAlert error={policy.error?.status === 404 ? "Nenhuma tarifa definida ainda. Só o Owner pode definir." : policy.error} />
          {policy.loading && <Skeleton className="h-24 w-full" />}
          {p && (
            <dl className="grid grid-cols-2 gap-4">
              <Item label="Taxa inicial" value={fmtMoney(p.minimumFee)} hint={`cobre ${p.minimumFeeCoverageMinutes} min`} />
              <Item label="Valor por hora" value={fmtMoney(p.hourlyRate)} hint="depois da cobertura" />
              <Item label="Carência" value={`${p.gracePeriodMinutes} min`} hint="saída sem cobrança" />
              <Item label="Vigente desde" value={fmtDate(p.effectiveFrom)} />
            </dl>
          )}
        </CardContent>
      </Card>
      <Missing title="Dados da empresa">Não existe entidade de empresa no sistema: nome, CNPJ e endereço precisam de rota nova.</Missing>
      <Missing title="Capacidade">Capacidade total, cota de mensalistas e free zone (C1, R1).</Missing>
      <Missing title="Regras de operação">Janela de saída após pagamento, 15 min (R2), e franquia do plano, 2 visitas/dia de 3h (N8).</Missing>
    </div>
  )
}

function Item({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="text-xl font-semibold tabular-nums">{value}</dd>
      {hint && <dd className="text-xs text-muted-foreground">{hint}</dd>}
    </div>
  )
}
