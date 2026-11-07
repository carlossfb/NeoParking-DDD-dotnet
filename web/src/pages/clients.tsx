import { useState } from "react"

import { ErrorAlert, SpecBadge } from "@/components/notice"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { api, ApiError, useApi, type Paged } from "@/lib/api"

type Client = {
  id: string
  name: string
  phoneNumber: string
  type: "Individual" | "Company"
  document: string // already masked by the API
  vehicles: { id: string; plate: string }[]
}

const typeLabel = { Individual: "Pessoa física", Company: "Empresa" }

export function Clients() {
  const [page, setPage] = useState(1)
  const list = useApi<Paged<Client>>(`/clients?page=${page}&pageSize=20`)
  const [found, setFound] = useState<Client[]>()
  const [searchError, setSearchError] = useState<string>()

  async function search(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const plate = String(new FormData(e.currentTarget).get("plate")).trim().toUpperCase()
    setSearchError(undefined)
    try {
      // POST with the plate in the body: plates never go in the URL.
      setFound([await api<Client>("/clients/search", "POST", { plate })])
    } catch (err) {
      setFound(undefined)
      setSearchError(err instanceof ApiError && err.status === 404 ? `Nenhum cliente com a placa ${plate}.` : (err as Error).message)
    }
  }

  const rows = found ?? list.data?.items
  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>Buscar cliente</CardTitle>
          <CardDescription>Placa exata. O valor vai no corpo da requisição, nunca na URL.</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={search}>
            <Field data-invalid={!!searchError} className="max-w-md">
              <FieldLabel htmlFor="plate">Placa</FieldLabel>
              <div className="flex gap-2">
                <Input id="plate" name="plate" required autoComplete="off" className="font-mono" aria-invalid={!!searchError} />
                <Button type="submit">Buscar</Button>
                {found && <Button type="button" variant="outline" onClick={() => setFound(undefined)}>Ver todos</Button>}
              </div>
              {searchError && <FieldError>{searchError}</FieldError>}
            </Field>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{found ? "Resultado da busca" : "Cadastrados"}</CardTitle>
          <CardDescription className="flex flex-wrap items-center gap-2">
            {found ? "1 cliente" : list.data ? `${list.data.totalCount} clientes · ordem de cadastro` : "Carregando…"}
            <SpecBadge>Mensalista: spec C4, não implementado</SpecBadge>
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <ErrorAlert error={list.error} />
          <div className="overflow-hidden rounded-lg border">
            <Table>
              <TableHeader className="bg-muted">
                <TableRow>
                  <TableHead>Nome</TableHead>
                  <TableHead>Tipo</TableHead>
                  <TableHead>Documento</TableHead>
                  <TableHead>Placas</TableHead>
                  <TableHead>Telefone</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {list.loading && !rows
                  ? [0, 1, 2].map((i) => (
                      <TableRow key={i}><TableCell colSpan={5}><Skeleton className="h-5 w-full" /></TableCell></TableRow>
                    ))
                  : rows?.map((c) => (
                      <TableRow key={c.id}>
                        <TableCell className="font-medium">{c.name}</TableCell>
                        <TableCell>{typeLabel[c.type] ?? c.type}</TableCell>
                        <TableCell className="font-mono">{c.document}</TableCell>
                        <TableCell className="font-mono">{c.vehicles.map((v) => v.plate).join(", ") || "—"}</TableCell>
                        <TableCell>{c.phoneNumber}</TableCell>
                      </TableRow>
                    ))}
                {rows?.length === 0 && (
                  <TableRow><TableCell colSpan={5} className="h-24 text-center text-muted-foreground">Nenhum cliente cadastrado ainda.</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </div>
          {!found && list.data && list.data.totalPages > 1 && (
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
