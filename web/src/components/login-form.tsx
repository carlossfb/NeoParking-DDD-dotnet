import { cn } from "@/lib/utils"

import { Button } from "@/components/ui/button"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Spinner } from "@/components/ui/spinner"

// login-02 block, without the GitHub, sign-up and forgot-password links: the API has none of those routes.
export function LoginForm({
  className,
  error,
  busy,
  ...props
}: React.ComponentProps<"form"> & { error?: string; busy?: boolean }) {
  return (
    <form className={cn("flex flex-col gap-6", className)} {...props}>
      <FieldGroup>
        <div className="flex flex-col items-center gap-1 text-center">
          <h1 className="text-2xl font-bold">Entre na sua conta</h1>
          <p className="text-sm text-balance text-muted-foreground">
            Use o e-mail e a senha do seu cadastro de operador
          </p>
        </div>
        <Field>
          <FieldLabel htmlFor="email">E-mail</FieldLabel>
          <Input id="email" name="email" type="email" autoComplete="username" placeholder="voce@empresa.com.br" required className="bg-background" />
        </Field>
        <Field data-invalid={!!error}>
          <FieldLabel htmlFor="password">Senha</FieldLabel>
          <Input id="password" name="password" type="password" autoComplete="current-password" required aria-invalid={!!error} className="bg-background" />
          {error && <FieldError>{error}</FieldError>}
        </Field>
        <Field>
          <Button type="submit" disabled={busy}>
            {busy && <Spinner />}
            Entrar
          </Button>
          <FieldDescription className="text-center">
            Sem acesso? Peça a um Owner ou Admin para criar sua conta.
          </FieldDescription>
        </Field>
      </FieldGroup>
    </form>
  )
}
