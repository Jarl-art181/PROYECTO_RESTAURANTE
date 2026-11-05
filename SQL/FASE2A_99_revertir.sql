/* =====================================================================
   FASE 2A · Script 99 — Revertir la migración 01 (sin restaurar .bak)
   Deja EMPLEADO como estaba antes de la FASE 2A.

     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -d AL_ESTILO_PIURA -I -f 65001
            -v SOLO_ENSAYO=1 -i FASE2A_99_revertir.sql

   Solo es posible si TODOS los empleados tienen usuario y contraseña
   (las columnas vuelven a ser NOT NULL). Si se registraron empleados
   sin cuenta, el script se detiene sin cambiar nada: en ese caso use el
   respaldo .bak o asígneles una cuenta primero.
   Se pierde el turno de los empleados que no son cajeros.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @SoloEnsayo BIT = $(SOLO_ENSAYO);

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.EMPLEADO', 'acceso_habilitado') IS NULL
        THROW 52101, 'La migración FASE 2A no está aplicada.', 1;

    IF EXISTS (SELECT 1 FROM dbo.EMPLEADO
               WHERE usuario IS NULL OR [contraseña] IS NULL)
        THROW 52102, 'Hay empleados sin cuenta: no se puede volver a NOT NULL. Use el respaldo .bak.', 1;

    DROP TRIGGER dbo.TR_CAJERO_TURNO_DESDE_EMPLEADO;
    DROP TRIGGER dbo.TR_EMPLEADO_TURNO_SINCRONIZA;

    ALTER TABLE dbo.EMPLEADO DROP CONSTRAINT CK_EMPLEADO_CUENTA;
    ALTER TABLE dbo.EMPLEADO DROP CONSTRAINT CK_EMPLEADO_TURNO;
    ALTER TABLE dbo.EMPLEADO DROP CONSTRAINT CK_EMPLEADO_INTENTOS;
    ALTER TABLE dbo.EMPLEADO DROP CONSTRAINT DF_EMPLEADO_ACCESO;
    ALTER TABLE dbo.EMPLEADO DROP CONSTRAINT DF_EMPLEADO_CAMBIO;
    ALTER TABLE dbo.EMPLEADO DROP CONSTRAINT DF_EMPLEADO_INTENTOS;

    ALTER TABLE dbo.EMPLEADO DROP COLUMN
        acceso_habilitado, debe_cambiar_contrasena,
        intentos_fallidos, bloqueo_hasta, turno;

    DROP INDEX UX_EMPLEADO_USUARIO ON dbo.EMPLEADO;

    ALTER TABLE dbo.EMPLEADO ALTER COLUMN usuario VARCHAR(50) NOT NULL;
    ALTER TABLE dbo.EMPLEADO ALTER COLUMN [contraseña] VARCHAR(255) NOT NULL;
    ALTER TABLE dbo.EMPLEADO ADD CONSTRAINT UQ_EMPLEADO_USU UNIQUE (usuario);

    IF @SoloEnsayo = 1
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'ENSAYO DE REVERSIÓN CORRECTO: no se cambió nada.';
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        PRINT 'MIGRACIÓN FASE 2A REVERTIDA.';
    END
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR: la reversión no se aplicó.';
    THROW;
END CATCH;
