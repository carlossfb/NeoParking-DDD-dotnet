-- Lista os dados que impedem a migration AddUniqueConstraints. Só leitura: não altera nada.
-- Rode antes do deploy. Corrija cada resultado manualmente; a migration nunca apaga dados.

-- Placa em mais de um veículo
SELECT 'vehicles.plate' AS rule_name, plate AS value_found, COUNT(*) AS occurrences
  FROM neoparking_access.vehicles GROUP BY plate HAVING COUNT(*) > 1;

-- CPF em mais de um cliente
SELECT 'clients.cpf' AS rule_name, cpf AS value_found, COUNT(*) AS occurrences
  FROM neoparking_access.clients GROUP BY cpf HAVING COUNT(*) > 1;

-- Mais de um ticket aberto para a mesma placa
SELECT 'tickets.open_plate' AS rule_name, plate AS value_found, COUNT(*) AS occurrences
  FROM neoparking_access.tickets WHERE Status = 'Open' GROUP BY plate HAVING COUNT(*) > 1;

-- Mais de um Owner
SELECT 'management_operators.owner' AS rule_name, Email AS value_found, Id AS occurrences
  FROM neoparking_management.management_operators
 WHERE Role = 'Owner' AND (SELECT COUNT(*) FROM neoparking_management.management_operators WHERE Role = 'Owner') > 1;

-- Mais de uma política de tarifação ativa
SELECT 'billing_pricing_policies.active' AS rule_name, Id AS value_found, EffectiveFrom AS occurrences
  FROM neoparking_billing.billing_pricing_policies
 WHERE EffectiveTo IS NULL AND (SELECT COUNT(*) FROM neoparking_billing.billing_pricing_policies WHERE EffectiveTo IS NULL) > 1;
