/* =====================================================================
   FASE 2A · Script 01 — Empleados sin cuenta, estado de cuenta y turno
   Base de datos: AL_ESTILO_PIURA (SQL Server)

   Ejecutar con sqlcmd (UTF-8 y QUOTED_IDENTIFIER activado):
     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -d AL_ESTILO_PIURA -I -f 65001
            -v SOLO_ENSAYO=1 -i FASE2A_01_empleados_cuentas.sql
   SOLO_ENSAYO=1 ejecuta todo y REVIERTE al final (prueba).
   SOLO_ENSAYO=0 confirma los cambios.

   Qué hace:
     1. usuario y [contraseña] pasan a admitir NULL (empleado sin cuenta).
     2. Unicidad de usuario mediante índice filtrado (ignora los NULL).
     3. Columnas de cuenta: acceso_habilitado, debe_cambiar_contrasena,
        intentos_fallidos, bloqueo_hasta.
     4. EMPLEADO.turno = ÚNICA FUENTE DE VERDAD del turno.
        CAJERO.turno se conserva como copia sincronizada por triggers.
     5. CK_EMPLEADO_CUENTA: una cuenta habilitada exige usuario y clave.
     6. Las 5 cuentas actuales quedan habilitadas; se copia el turno
        del cajero a EMPLEADO.turno.
   No borra filas ni toca pedidos, pagos ni comprobantes.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @SoloEnsayo BIT = $(SOLO_ENSAYO);

BEGIN TRY
    BEGIN TRANSACTION;

    /* ---------- 0. Comprobaciones previas ---------- */
    IF COL_LENGTH('dbo.EMPLEADO', 'acceso_habilitado') IS NOT NULL
        THROW 52001, 'La migración FASE 2A ya fue aplicada.', 1;

    IF EXISTS (SELECT 1 FROM dbo.EMPLEADO
               WHERE usuario IS NULL OR [contraseña] IS NULL)
        THROW 52002, 'Hay empleados sin usuario o contraseña: revisar antes de migrar.', 1;

    IF EXISTS (SELECT usuario FROM dbo.EMPLEADO
               GROUP BY usuario HAVING COUNT(*) > 1)
        THROW 52003, 'Hay usuarios repetidos: revisar antes de migrar.', 1;

    IF OBJECT_ID('dbo.UQ_EMPLEADO_USU', 'UQ') IS NULL
        THROW 52004, 'No existe la restricción UQ_EMPLEADO_USU esperada.', 1;

    DECLARE @Antes TABLE (tabla SYSNAME, filas INT);
    INSERT INTO @Antes
    SELECT 'EMPLEADO', COUNT(*) FROM dbo.EMPLEADO UNION ALL
    SELECT 'PEDIDO', COUNT(*) FROM dbo.PEDIDO UNION ALL
    SELECT 'PAGO', COUNT(*) FROM dbo.PAGO UNION ALL
    SELECT 'COMPROBANTE', COUNT(*) FROM dbo.COMPROBANTE UNION ALL
    SELECT 'CAJERO', COUNT(*) FROM dbo.CAJERO;

    /* ---------- 1. Usuario y contraseña opcionales ---------- */
    ALTER TABLE dbo.EMPLEADO DROP CONSTRAINT UQ_EMPLEADO_USU;
    ALTER TABLE dbo.EMPLEADO ALTER COLUMN usuario VARCHAR(50) NULL;
    ALTER TABLE dbo.EMPLEADO ALTER COLUMN [contraseña] VARCHAR(255) NULL;

    /* ---------- 2. Usuario único (solo cuando existe) ---------- */
    CREATE UNIQUE NONCLUSTERED INDEX UX_EMPLEADO_USUARIO
        ON dbo.EMPLEADO (usuario)
        WHERE usuario IS NOT NULL;

    /* ---------- 3. Estado de la cuenta y turno ---------- */
    ALTER TABLE dbo.EMPLEADO ADD
        acceso_habilitado       BIT       NOT NULL CONSTRAINT DF_EMPLEADO_ACCESO   DEFAULT (0),
        debe_cambiar_contrasena BIT       NOT NULL CONSTRAINT DF_EMPLEADO_CAMBIO   DEFAULT (0),
        intentos_fallidos       INT       NOT NULL CONSTRAINT DF_EMPLEADO_INTENTOS DEFAULT (0),
        bloqueo_hasta           DATETIME2 NULL,
        turno                   VARCHAR(20) NULL;

    /* Las sentencias siguientes usan columnas recién creadas:
       se ejecutan con EXEC para que se compilen después del ALTER. */

    EXEC (N'
        ALTER TABLE dbo.EMPLEADO ADD CONSTRAINT CK_EMPLEADO_TURNO
            CHECK (turno IN (''mañana'', ''tarde'', ''noche''));

        ALTER TABLE dbo.EMPLEADO ADD CONSTRAINT CK_EMPLEADO_INTENTOS
            CHECK (intentos_fallidos >= 0);');

    /* ---------- 4. Conservar las cuentas actuales ---------- */
    EXEC (N'
        UPDATE dbo.EMPLEADO
           SET acceso_habilitado = 1
         WHERE usuario IS NOT NULL AND [contraseña] IS NOT NULL;');

    /* Ahora sí puede exigirse la coherencia de la cuenta. */
    EXEC (N'
        ALTER TABLE dbo.EMPLEADO ADD CONSTRAINT CK_EMPLEADO_CUENTA
            CHECK (acceso_habilitado = 0
                   OR (usuario IS NOT NULL AND [contraseña] IS NOT NULL));');

    /* ---------- 5. Turno: migrar desde CAJERO ---------- */
    EXEC (N'
        UPDATE E
           SET turno = C.turno
          FROM dbo.EMPLEADO E
          JOIN dbo.CAJERO   C ON C.id_empleado = E.id_empleado
         WHERE C.turno IS NOT NULL;');

    /* ---------- 6. Sincronización EMPLEADO.turno -> CAJERO.turno ---------- */
    EXEC (N'
CREATE TRIGGER dbo.TR_EMPLEADO_TURNO_SINCRONIZA
ON dbo.EMPLEADO
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    -- EMPLEADO.turno es la fuente de verdad; CAJERO.turno es una copia.
    IF UPDATE(turno)
        UPDATE C
           SET turno = i.turno
          FROM dbo.CAJERO C
          JOIN inserted   i ON i.id_empleado = C.id_empleado
         WHERE ISNULL(C.turno, '''') <> ISNULL(i.turno, '''');
END;');

    EXEC (N'
CREATE TRIGGER dbo.TR_CAJERO_TURNO_DESDE_EMPLEADO
ON dbo.CAJERO
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    -- Un cajero nuevo o modificado toma siempre el turno de EMPLEADO.
    UPDATE C
       SET turno = E.turno
      FROM dbo.CAJERO   C
      JOIN inserted     i ON i.id_empleado = C.id_empleado
      JOIN dbo.EMPLEADO E ON E.id_empleado = C.id_empleado
     WHERE ISNULL(C.turno, '''') <> ISNULL(E.turno, '''');
END;');

    /* ---------- 7. Verificaciones dentro de la transacción ---------- */
    DECLARE @Despues TABLE (tabla SYSNAME, filas INT);
    INSERT INTO @Despues
    SELECT 'EMPLEADO', COUNT(*) FROM dbo.EMPLEADO UNION ALL
    SELECT 'PEDIDO', COUNT(*) FROM dbo.PEDIDO UNION ALL
    SELECT 'PAGO', COUNT(*) FROM dbo.PAGO UNION ALL
    SELECT 'COMPROBANTE', COUNT(*) FROM dbo.COMPROBANTE UNION ALL
    SELECT 'CAJERO', COUNT(*) FROM dbo.CAJERO;

    IF EXISTS (SELECT 1 FROM @Antes a JOIN @Despues d ON d.tabla = a.tabla
               WHERE a.filas <> d.filas)
        THROW 52010, 'La cantidad de filas cambió durante la migración.', 1;

    EXEC (N'
        IF EXISTS (SELECT 1 FROM dbo.EMPLEADO WHERE acceso_habilitado <> 1)
            THROW 52011, ''Alguna cuenta existente no quedó habilitada.'', 1;

        IF EXISTS (SELECT 1 FROM dbo.CAJERO C
                   JOIN dbo.EMPLEADO E ON E.id_empleado = C.id_empleado
                   WHERE ISNULL(C.turno, '''') <> ISNULL(E.turno, ''''))
            THROW 52012, ''El turno de CAJERO y EMPLEADO no coincide.'', 1;');

    SELECT a.tabla, a.filas AS filas_antes, d.filas AS filas_despues
      FROM @Antes a JOIN @Despues d ON d.tabla = a.tabla;

    EXEC (N'
        SELECT E.id_empleado, E.usuario, E.estado, E.acceso_habilitado,
               E.debe_cambiar_contrasena, E.turno AS turno_empleado,
               C.turno AS turno_cajero
          FROM dbo.EMPLEADO E
          LEFT JOIN dbo.CAJERO C ON C.id_empleado = E.id_empleado
         ORDER BY E.id_empleado;');

    IF @SoloEnsayo = 1
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'ENSAYO CORRECTO: todos los cambios fueron revertidos.';
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        PRINT 'MIGRACIÓN FASE 2A APLICADA CORRECTAMENTE.';
    END
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'ERROR: la migración se revirtió por completo.';
    THROW;
END CATCH;
