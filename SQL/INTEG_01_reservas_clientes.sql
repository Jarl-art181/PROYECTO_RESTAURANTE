/* ==========================================================
   INTEGRACIÓN MESAS - RESERVAS - CLIENTES - PEDIDOS - RECIBO
   Cambios aprobados el 2026-11-06.

   * RESERVA_MESA.id_cliente : cliente registrado que hizo la reserva.
       Opcional; solo se llena si el usuario confirma la coincidencia
       (nunca por nombres parecidos).
   * PEDIDO.id_reserva       : reserva que originó el pedido de salón.
       Opcional; índice único filtrado: una reserva origina como
       máximo un pedido.
   * CLIENTE.acepta_promociones   : consentimiento explícito para
       comunicaciones comerciales (0 = no; valor por defecto).
     CLIENTE.fecha_consentimiento : fecha del último cambio de ese
       consentimiento (otorgado o retirado).
   * No modifica filas existentes: reservas y pedidos actuales quedan
     sin vínculo; los clientes actuales quedan SIN consentimiento.
   * COMPROBANTE no cambia.
   * Idempotente. Reversión: INTEG_99_revertir.sql

   Ejecutar (primero en AL_ESTILO_PIURA_PRUEBAS):
     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -I -f 65001 -d <base> -i INTEG_01_reservas_clientes.sql
   ========================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.RESERVA_MESA', 'id_cliente') IS NULL
    ALTER TABLE dbo.RESERVA_MESA ADD id_cliente INT NULL;

IF COL_LENGTH('dbo.PEDIDO', 'id_reserva') IS NULL
    ALTER TABLE dbo.PEDIDO ADD id_reserva INT NULL;

IF COL_LENGTH('dbo.CLIENTE', 'acepta_promociones') IS NULL
    ALTER TABLE dbo.CLIENTE ADD acepta_promociones BIT NOT NULL
        CONSTRAINT DF_CLIENTE_PROMOS DEFAULT (0);

IF COL_LENGTH('dbo.CLIENTE', 'fecha_consentimiento') IS NULL
    ALTER TABLE dbo.CLIENTE ADD fecha_consentimiento DATETIME2(0) NULL;

COMMIT TRANSACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RESERVA_CLIENTE')
    ALTER TABLE dbo.RESERVA_MESA ADD CONSTRAINT FK_RESERVA_CLIENTE
        FOREIGN KEY (id_cliente) REFERENCES dbo.CLIENTE (id_cliente);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RESERVA_CLIENTE')
    CREATE INDEX IX_RESERVA_CLIENTE ON dbo.RESERVA_MESA (id_cliente) WHERE id_cliente IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PEDIDO_RESERVA')
    ALTER TABLE dbo.PEDIDO ADD CONSTRAINT FK_PEDIDO_RESERVA
        FOREIGN KEY (id_reserva) REFERENCES dbo.RESERVA_MESA (id_reserva);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PEDIDO_RESERVA')
    CREATE UNIQUE INDEX UX_PEDIDO_RESERVA ON dbo.PEDIDO (id_reserva) WHERE id_reserva IS NOT NULL;

-- Con consentimiento debe constar la fecha en que se otorgó.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CLIENTE_CONSENTIMIENTO')
    ALTER TABLE dbo.CLIENTE ADD CONSTRAINT CK_CLIENTE_CONSENTIMIENTO
        CHECK (acepta_promociones = 0 OR fecha_consentimiento IS NOT NULL);
GO

-- Verificación
SELECT OBJECT_NAME(c.object_id) AS tabla, c.name AS columna, t.name AS tipo, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE (c.object_id = OBJECT_ID('dbo.RESERVA_MESA') AND c.name = 'id_cliente')
   OR (c.object_id = OBJECT_ID('dbo.PEDIDO') AND c.name = 'id_reserva')
   OR (c.object_id = OBJECT_ID('dbo.CLIENTE') AND c.name IN ('acepta_promociones', 'fecha_consentimiento'));

SELECT (SELECT COUNT(*) FROM dbo.RESERVA_MESA) AS reservas,
       (SELECT COUNT(*) FROM dbo.PEDIDO) AS pedidos,
       (SELECT COUNT(*) FROM dbo.CLIENTE) AS clientes,
       (SELECT COUNT(*) FROM dbo.CLIENTE WHERE acepta_promociones = 1) AS con_consentimiento,
       (SELECT COUNT(*) FROM dbo.COMPROBANTE) AS comprobantes;
