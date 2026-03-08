-- =============================================================================
-- SCRIPT: Aplica columnas faltantes en la base de datos (Simone)
-- Ejecutar una sola vez contra la BD de SQL Server.
-- Todas las sentencias son idempotentes (solo agregan si la columna no existe).
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. SubPedidos.FechaEntrega
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'SubPedidos') AND name = N'FechaEntrega'
)
BEGIN
    ALTER TABLE SubPedidos ADD FechaEntrega datetime2 NULL;
    PRINT 'SubPedidos.FechaEntrega → AGREGADA';
END
ELSE
    PRINT 'SubPedidos.FechaEntrega → ya existe (omitida)';

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. EnviosConsolidados.FechaEntrega
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'EnviosConsolidados') AND name = N'FechaEntrega'
)
BEGIN
    ALTER TABLE EnviosConsolidados ADD FechaEntrega datetime2 NULL;
    PRINT 'EnviosConsolidados.FechaEntrega → AGREGADA';
END
ELSE
    PRINT 'EnviosConsolidados.FechaEntrega → ya existe (omitida)';

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. Vendedores — columnas de perfil/tienda
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Vendedores') AND name = N'Slug'
)
BEGIN
    ALTER TABLE Vendedores ADD Slug nvarchar(100) NULL;
    PRINT 'Vendedores.Slug → AGREGADA';
END
ELSE
    PRINT 'Vendedores.Slug → ya existe (omitida)';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Vendedores') AND name = N'Bio'
)
BEGIN
    ALTER TABLE Vendedores ADD Bio nvarchar(500) NULL;
    PRINT 'Vendedores.Bio → AGREGADA';
END
ELSE
    PRINT 'Vendedores.Bio → ya existe (omitida)';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Vendedores') AND name = N'BannerPath'
)
BEGIN
    ALTER TABLE Vendedores ADD BannerPath nvarchar(300) NULL;
    PRINT 'Vendedores.BannerPath → AGREGADA';
END
ELSE
    PRINT 'Vendedores.BannerPath → ya existe (omitida)';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Vendedores') AND name = N'Verificado'
)
BEGIN
    ALTER TABLE Vendedores ADD Verificado bit NOT NULL DEFAULT 0;
    PRINT 'Vendedores.Verificado → AGREGADA';
END
ELSE
    PRINT 'Vendedores.Verificado → ya existe (omitida)';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Vendedores') AND name = N'InstagramUrl'
)
BEGIN
    ALTER TABLE Vendedores ADD InstagramUrl nvarchar(200) NULL;
    PRINT 'Vendedores.InstagramUrl → AGREGADA';
END
ELSE
    PRINT 'Vendedores.InstagramUrl → ya existe (omitida)';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Vendedores') AND name = N'TikTokUrl'
)
BEGIN
    ALTER TABLE Vendedores ADD TikTokUrl nvarchar(200) NULL;
    PRINT 'Vendedores.TikTokUrl → AGREGADA';
END
ELSE
    PRINT 'Vendedores.TikTokUrl → ya existe (omitida)';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Vendedores') AND name = N'FacebookUrl'
)
BEGIN
    ALTER TABLE Vendedores ADD FacebookUrl nvarchar(200) NULL;
    PRINT 'Vendedores.FacebookUrl → AGREGADA';
END
ELSE
    PRINT 'Vendedores.FacebookUrl → ya existe (omitida)';

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. ReservasStock — tabla nueva (si no existe)
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReservasStock')
BEGIN
    CREATE TABLE ReservasStock (
        ReservaStockId      int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductoID          int NOT NULL,
        ProductoVarianteID  int NULL,
        Cantidad            int NOT NULL,
        UsuarioId           nvarchar(450) NULL,
        Canal               nvarchar(20) NOT NULL,
        SesionPosId         nvarchar(64) NULL,
        FechaCreacion       datetime2 NOT NULL,
        Expiracion          datetime2 NOT NULL,
        Confirmada          bit NOT NULL DEFAULT 0,

        CONSTRAINT FK_ReservasStock_Productos
            FOREIGN KEY (ProductoID) REFERENCES Productos(ProductoID) ON DELETE CASCADE,

        CONSTRAINT FK_ReservasStock_ProductoVariantes
            FOREIGN KEY (ProductoVarianteID) REFERENCES ProductoVariantes(ProductoVarianteID)
    );

    CREATE INDEX IX_ReservasStock_ProductoID          ON ReservasStock(ProductoID);
    CREATE INDEX IX_ReservasStock_ProductoVarianteID  ON ReservasStock(ProductoVarianteID);

    PRINT 'Tabla ReservasStock → CREADA';
END
ELSE
    PRINT 'Tabla ReservasStock → ya existe (omitida)';

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. Registrar migraciones pendientes en __EFMigrationsHistory
--    (para que EF Core no intente re-aplicarlas con dotnet ef database update)
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260307000001_AddFechaEntregaToSubPedido'
)
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260307000001_AddFechaEntregaToSubPedido', '9.0.0');
    PRINT 'Migration AddFechaEntregaToSubPedido → REGISTRADA';
END

IF NOT EXISTS (
    SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260307055456_posible'
)
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260307055456_posible', '9.0.0');
    PRINT 'Migration posible → REGISTRADA';
END

IF NOT EXISTS (
    SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260307060411_FixDecimalTypes'
)
BEGIN
    -- Aplica los cambios de FixDecimalTypes
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CuponesUsados_Promociones_PromocionID1'
    )
    BEGIN
        ALTER TABLE CuponesUsados DROP CONSTRAINT FK_CuponesUsados_Promociones_PromocionID1;
    END

    IF EXISTS (
        SELECT 1 FROM sys.indexes WHERE name = 'IX_CuponesUsados_PromocionID1'
    )
    BEGIN
        DROP INDEX IX_CuponesUsados_PromocionID1 ON CuponesUsados;
    END

    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'CuponesUsados') AND name = N'PromocionID1'
    )
    BEGIN
        ALTER TABLE CuponesUsados DROP COLUMN PromocionID1;
        PRINT 'CuponesUsados.PromocionID1 → ELIMINADA';
    END

    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260307060411_FixDecimalTypes', '9.0.0');
    PRINT 'Migration FixDecimalTypes → REGISTRADA';
END

IF NOT EXISTS (
    SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260308060609_pruebass'
)
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260308060609_pruebass', '9.0.0');
    PRINT 'Migration pruebass → REGISTRADA';
END

PRINT '';
PRINT '✅ Script completado. Reinicia la aplicación.';
