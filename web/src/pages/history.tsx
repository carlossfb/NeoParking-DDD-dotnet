import { Missing } from "@/components/notice"

export function History() {
  return (
    <Missing title="Comparar períodos">
      Entradas, saídas, permanência média e receita estimada de dois períodos lado a lado precisam de uma rota de agregação, que a API ainda não tem.
      Ler os tickets página por página não escala: o limite é de 100 por página.
    </Missing>
  )
}
