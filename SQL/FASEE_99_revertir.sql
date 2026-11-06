/* ==========================================================
   FASE E - REVERSIÓN
   Quita las columnas agregadas por FASEE_01_inventario.sql.
   ATENCIÓN: se pierden los estados, categorías y descripciones
   de insumos registrados desde la Fase E. El stock, las compras
   y las mermas no se modifican. Haga un respaldo antes.
   ========================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_INSUMO_ESTADO')
    ALTER TABLE dbo.INSUMO DROP CONSTRAINT CK_INSUMO_ESTADO;
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_INSUMO_ESTADO')
    ALTER TABLE dbo.INSUMO DROP CONSTRAINT DF_INSUMO_ESTADO;

IF COL_LENGTH('dbo.INSUMO', 'descripcion') IS NOT NULL ALTER TABLE dbo.INSUMO DROP COLUMN descripcion;
IF COL_LENGTH('dbo.INSUMO', 'categoria') IS NOT NULL ALTER TABLE dbo.INSUMO DROP COLUMN categoria;
IF COL_LENGTH('dbo.INSUMO', 'estado') IS NOT NULL ALTER TABLE dbo.INSUMO DROP COLUMN estado;

COMMIT TRANSACTION;
GO
