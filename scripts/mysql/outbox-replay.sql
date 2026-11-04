-- Devolve à fila as mensagens em dead-letter. Rode só depois de corrigir a causa em LastError.
--
-- Para reenviar uma mensagem específica, troque a condição por Id = '<guid>'.
-- Os handlers são idempotentes, então a reentrega não duplica efeito. As mensagens saem em ordem
-- de CreatedAt: reenvie juntas as que têm relação, como TicketOpened e TicketClosed do mesmo ticket.
UPDATE neoparking_access.outbox_messages
   SET DeadLetteredAt = NULL, NextAttemptAt = NULL, AttemptCount = 0
 WHERE DeadLetteredAt IS NOT NULL;

UPDATE neoparking_management.management_outbox_messages
   SET DeadLetteredAt = NULL, NextAttemptAt = NULL, AttemptCount = 0
 WHERE DeadLetteredAt IS NOT NULL;

UPDATE neoparking_billing.billing_outbox_messages
   SET DeadLetteredAt = NULL, NextAttemptAt = NULL, AttemptCount = 0
 WHERE DeadLetteredAt IS NOT NULL;
