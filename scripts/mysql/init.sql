-- Cria os schemas de cada BC com isolamento de banco.
-- As migrations do EF Core criam as tabelas dentro de cada schema.
CREATE DATABASE IF NOT EXISTS neoparking_access     CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS neoparking_management CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
