/* ==========================================================
   FASE C - REVERSIÓN
   Quita las columnas agregadas por FASEC_01_clientes.sql.
   ATENCIÓN: se pierden los correos, fechas de nacimiento,
   distritos, comentarios y estados registrados desde la Fase C.
   Haga un respaldo antes de ejecutarlo.
   ========================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CLIENTE_ESTADO')
    ALTER TABLE dbo.CLIENTE DROP CONSTRAINT CK_CLIENTE_ESTADO;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CLIENTE_FECHA_NAC')
    ALTER TABLE dbo.CLIENTE DROP CONSTRAINT CK_CLIENTE_FECHA_NAC;
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CLIENTE_ESTADO')
    ALTER TABLE dbo.CLIENTE DROP CONSTRAINT DF_CLIENTE_ESTADO;

IF COL_LENGTH('dbo.CLIENTE', 'estado') IS NOT NULL ALTER TABLE dbo.CLIENTE DROP COLUMN estado;
IF COL_LENGTH('dbo.CLIENTE', 'comentario') IS NOT NULL ALTER TABLE dbo.CLIENTE DROP COLUMN comentario;
IF COL_LENGTH('dbo.CLIENTE', 'distrito') IS NOT NULL ALTER TABLE dbo.CLIENTE DROP COLUMN distrito;
IF COL_LENGTH('dbo.CLIENTE', 'fecha_nacimiento') IS NOT NULL ALTER TABLE dbo.CLIENTE DROP COLUMN fecha_nacimiento;
IF COL_LENGTH('dbo.CLIENTE', 'correo') IS NOT NULL ALTER TABLE dbo.CLIENTE DROP COLUMN correo;

COMMIT TRANSACTION;
GO
