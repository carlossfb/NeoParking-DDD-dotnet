# NeoParking web

React + Vite + shadcn/ui (estilo Nova, blocos `dashboard-01` e `login-02`). O tema fica todo em `src/index.css`: paleta 60-30-10 em variáveis oklch, com modo claro e escuro.

## Rodar

1. Suba a API (veja o README da raiz). Em desenvolvimento ela escuta em `http://localhost:5114`.
2. Neste diretório:

```bash
npm install
npm run dev        # http://localhost:5173
```

O Vite repassa `/api/*` para a API (`API_URL` troca o destino, por exemplo `API_URL=http://localhost:8080` com o Docker Compose). A API não tem CORS, então em produção o front precisa ser servido no mesmo domínio, atrás de um proxy.

```bash
npm test           # node:test, sem dependência extra
npm run build
```

## O que está ligado à API

| Tela | Rotas |
|---|---|
| Login | `POST /operators/login` |
| Visão geral | `GET /access/tickets` (abertos, entradas de hoje), `GET /billing/tickets/fee-history` (Owner, Admin) |
| Controle de acesso | `GET /access/tickets`, `POST /access/tickets`, `POST /access/tickets/by-code/{code}/exit` |
| Clientes | `GET /clients`, `POST /clients/search` |
| Fluxo de caixa | `GET /billing/tickets/fee-history` (Owner, Admin) |
| Empresa | `GET /billing/pricing-policies/active` |

Histórico, saídas do dia, mensalistas, dados da empresa e capacidade aparecem como "Spec, não implementado": dependem de rotas que a API ainda não tem.
