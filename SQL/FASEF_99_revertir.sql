/* ==========================================================
   FASE F - REVERSIÓN
   Quita las columnas agregadas por FASEF_01_promociones.sql.
   ATENCIÓN: se pierden las descripciones, días y montos mínimos
   de las promociones. Pedidos, descuentos y cobros no se tocan.
   Haga un respaldo antes de ejecutarlo.
   ========================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PROMO_DIAS')
    ALTER TABLE dbo.PROMOCION DROP CONSTRAINT CK_PROMO_DIAS;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PROMO_MINIMO')
    ALTER TABLE dbo.PROMOCION DROP CONSTRAINT CK_PROMO_MINIMO;

IF COL_LENGTH('dbo.PROMOCION', 'monto_minimo') IS NOT NULL ALTER TABLE dbo.PROMOCION DROP COLUMN monto_minimo;
IF COL_LENGTH('dbo.PROMOCION', 'dias_semana') IS NOT NULL ALTER TABLE dbo.PROMOCION DROP COLUMN dias_semana;
IF COL_LENGTH('dbo.PROMOCION', 'descripcion') IS NOT NULL ALTER TABLE dbo.PROMOCION DROP COLUMN descripcion;

COMMIT TRANSACTION;
GO
