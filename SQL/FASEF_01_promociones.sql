/* ==========================================================
   FASE F - GESTIÓN DE PROMOCIONES
   Agrega a PROMOCION los datos aprobados el 2026-11-06.

   * descripcion : opcional.
   * dias_semana : opcional. Días ISO 8601 como dígitos:
                   1 = lunes, 2 = martes, 3 = miércoles, 4 = jueves,
                   5 = viernes, 6 = sábado, 7 = domingo.
                   Ej.: '2' = solo martes; '135' = lun, mié y vie.
                   NULL = todos los días.
                   No depende de SET DATEFIRST ni del idioma. En SQL
                   el día ISO de una fecha es:
                     DATEDIFF(DAY, '19000101', @fecha) % 7 + 1
                   (el 01/01/1900 fue lunes).
   * monto_minimo: opcional; subtotal mínimo del pedido (sin propina)
                   para poder aplicar la promoción. NULL = sin mínimo.
   * No modifica ninguna fila: las promociones actuales quedan con
     las tres columnas en NULL (sin restricción adicional).
   * Idempotente. Reversión: FASEF_99_revertir.sql

   Ejecutar (primero en AL_ESTILO_PIURA_PRUEBAS):
     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -I -f 65001 -d <base> -i FASEF_01_promociones.sql
   ========================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.PROMOCION', 'descripcion') IS NULL
    ALTER TABLE dbo.PROMOCION ADD descripcion VARCHAR(255) NULL;

IF COL_LENGTH('dbo.PROMOCION', 'dias_semana') IS NULL
    ALTER TABLE dbo.PROMOCION ADD dias_semana VARCHAR(7) NULL;

IF COL_LENGTH('dbo.PROMOCION', 'monto_minimo') IS NULL
    ALTER TABLE dbo.PROMOCION ADD monto_minimo DECIMAL(10,2) NULL;

COMMIT TRANSACTION;
GO

-- Días: solo dígitos 1-7, de 1 a 7 caracteres y sin repetir ninguno.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PROMO_DIAS')
    ALTER TABLE dbo.PROMOCION ADD CONSTRAINT CK_PROMO_DIAS CHECK (
        dias_semana IS NULL OR (
            LEN(dias_semana) BETWEEN 1 AND 7
            AND dias_semana NOT LIKE '%[^1-7]%'
            AND LEN(dias_semana) - LEN(REPLACE(dias_semana, '1', '')) <= 1
            AND LEN(dias_semana) - LEN(REPLACE(dias_semana, '2', '')) <= 1
            AND LEN(dias_semana) - LEN(REPLACE(dias_semana, '3', '')) <= 1
            AND LEN(dias_semana) - LEN(REPLACE(dias_semana, '4', '')) <= 1
            AND LEN(dias_semana) - LEN(REPLACE(dias_semana, '5', '')) <= 1
            AND LEN(dias_semana) - LEN(REPLACE(dias_semana, '6', '')) <= 1
            AND LEN(dias_semana) - LEN(REPLACE(dias_semana, '7', '')) <= 1));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PROMO_MINIMO')
    ALTER TABLE dbo.PROMOCION ADD CONSTRAINT CK_PROMO_MINIMO
        CHECK (monto_minimo IS NULL OR monto_minimo >= 0);
GO

-- Verificación
SELECT c.name AS columna, t.name AS tipo, c.max_length, c.precision, c.scale, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.PROMOCION')
ORDER BY c.column_id;

SELECT COUNT(*) AS promociones FROM dbo.PROMOCION;
