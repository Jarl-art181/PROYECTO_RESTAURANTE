/* ==========================================================
   FASE C - GESTIÓN DE CLIENTES
   Agrega a CLIENTE los datos que pide el formulario existente.
   Aprobado por el propietario el 2026-11-06.

   * correo, fecha_nacimiento, distrito, comentario: opcionales (NULL).
   * estado: 'activo' / 'inactivo', por defecto 'activo'.
     "Eliminar" en la aplicación DESACTIVA (no borra): los pedidos,
     canjes y puntos del cliente se conservan.
   * No modifica ni borra ninguna fila existente: los clientes
     actuales quedan 'activo' y con los campos nuevos en NULL.
   * Idempotente: se puede ejecutar más de una vez.
   * Reversión: FASEC_99_revertir.sql

   Ejecutar (primero en AL_ESTILO_PIURA_PRUEBAS):
     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -I -f 65001 -d <base> -i FASEC_01_clientes.sql
   ========================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.CLIENTE', 'correo') IS NULL
    ALTER TABLE dbo.CLIENTE ADD correo VARCHAR(100) NULL;

IF COL_LENGTH('dbo.CLIENTE', 'fecha_nacimiento') IS NULL
    ALTER TABLE dbo.CLIENTE ADD fecha_nacimiento DATE NULL;

IF COL_LENGTH('dbo.CLIENTE', 'distrito') IS NULL
    ALTER TABLE dbo.CLIENTE ADD distrito VARCHAR(100) NULL;

IF COL_LENGTH('dbo.CLIENTE', 'comentario') IS NULL
    ALTER TABLE dbo.CLIENTE ADD comentario VARCHAR(255) NULL;

IF COL_LENGTH('dbo.CLIENTE', 'estado') IS NULL
    ALTER TABLE dbo.CLIENTE ADD estado VARCHAR(20) NOT NULL
        CONSTRAINT DF_CLIENTE_ESTADO DEFAULT ('activo');

COMMIT TRANSACTION;
GO

-- Restricciones (en un lote aparte: las columnas ya existen).
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CLIENTE_ESTADO')
    ALTER TABLE dbo.CLIENTE ADD CONSTRAINT CK_CLIENTE_ESTADO
        CHECK (estado IN ('activo', 'inactivo'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CLIENTE_FECHA_NAC')
    ALTER TABLE dbo.CLIENTE ADD CONSTRAINT CK_CLIENTE_FECHA_NAC
        CHECK (fecha_nacimiento IS NULL OR fecha_nacimiento >= '19000101');
GO

-- Verificación
SELECT c.name AS columna, t.name AS tipo, c.max_length, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.CLIENTE')
ORDER BY c.column_id;

SELECT estado, COUNT(*) AS clientes FROM dbo.CLIENTE GROUP BY estado;
