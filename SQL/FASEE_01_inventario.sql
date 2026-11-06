/* ==========================================================
   FASE E - GESTIÓN DE INVENTARIO
   Agrega a INSUMO los datos que pide el formulario existente.
   Aprobado por el propietario el 2026-11-06.

   * estado: 'activo' / 'inactivo', por defecto 'activo'.
     "Eliminar" en la aplicación DESACTIVA el insumo (no se
     borran compras, mermas ni recetas que lo usan).
   * categoria: opcional (Carnes, Verduras, Abarrotes...).
   * descripcion: opcional.
   * No modifica ninguna fila existente: los insumos actuales
     quedan 'activo' y con categoría y descripción en NULL.
   * No toca stock_actual: lo siguen manteniendo los triggers
     TR_DETALLE_COMPRA_STOCK y TR_MERMA_STOCK.
   * Idempotente: se puede ejecutar más de una vez.
   * Reversión: FASEE_99_revertir.sql

   Ejecutar (primero en AL_ESTILO_PIURA_PRUEBAS):
     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -I -f 65001 -d <base> -i FASEE_01_inventario.sql
   ========================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.INSUMO', 'estado') IS NULL
    ALTER TABLE dbo.INSUMO ADD estado VARCHAR(20) NOT NULL
        CONSTRAINT DF_INSUMO_ESTADO DEFAULT ('activo');

IF COL_LENGTH('dbo.INSUMO', 'categoria') IS NULL
    ALTER TABLE dbo.INSUMO ADD categoria VARCHAR(50) NULL;

IF COL_LENGTH('dbo.INSUMO', 'descripcion') IS NULL
    ALTER TABLE dbo.INSUMO ADD descripcion VARCHAR(255) NULL;

COMMIT TRANSACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_INSUMO_ESTADO')
    ALTER TABLE dbo.INSUMO ADD CONSTRAINT CK_INSUMO_ESTADO
        CHECK (estado IN ('activo', 'inactivo'));
GO

-- Verificación
SELECT c.name AS columna, t.name AS tipo, c.max_length, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.INSUMO')
ORDER BY c.column_id;

SELECT estado, COUNT(*) AS insumos FROM dbo.INSUMO GROUP BY estado;
