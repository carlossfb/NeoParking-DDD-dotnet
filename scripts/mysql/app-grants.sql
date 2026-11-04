-- Acesso do usuário da aplicação, restrito aos schemas de cada BC.
-- O usuário é criado antes deste script: no compose, por MYSQL_USER e MYSQL_PASSWORD.
GRANT ALL PRIVILEGES ON neoparking_access.*     TO 'neoparking_app'@'%';
GRANT ALL PRIVILEGES ON neoparking_management.* TO 'neoparking_app'@'%';
GRANT ALL PRIVILEGES ON neoparking_billing.*    TO 'neoparking_app'@'%';
