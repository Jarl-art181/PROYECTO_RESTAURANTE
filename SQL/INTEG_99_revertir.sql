/* ==========================================================
   INTEGRACIÓN RESERVAS - CLIENTES - REVERSIÓN
   Quita lo agregado por INTEG_01_reservas_clientes.sql.
   ATENCIÓN: se pierden los vínculos reserva-cliente y
   pedido-reserva y los consentimientos de promociones.
   Pedidos, reservas, cobros y comprobantes no se borran.
   Haga un respaldo antes de ejecutarlo.
   ========================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CLIENTE_CONSENTIMIENTO')
    ALTER TABLE dbo.CLIENTE DROP CONSTRAINT CK_CLIENTE_CONSENTIMIENTO;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PEDIDO_RESERVA')
    DROP INDEX UX_PEDIDO_RESERVA ON dbo.PEDIDO;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PEDIDO_RESERVA')
    ALTER TABLE dbo.PEDIDO DROP CONSTRAINT FK_PEDIDO_RESERVA;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RESERVA_CLIENTE')
    DROP INDEX IX_RESERVA_CLIENTE ON dbo.RESERVA_MESA;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RESERVA_CLIENTE')
    ALTER TABLE dbo.RESERVA_MESA DROP CONSTRAINT FK_RESERVA_CLIENTE;
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CLIENTE_PROMOS')
    ALTER TABLE dbo.CLIENTE DROP CONSTRAINT DF_CLIENTE_PROMOS;

IF COL_LENGTH('dbo.CLIENTE', 'fecha_consentimiento') IS NOT NULL ALTER TABLE dbo.CLIENTE DROP COLUMN fecha_consentimiento;
IF COL_LENGTH('dbo.CLIENTE', 'acepta_promociones') IS NOT NULL ALTER TABLE dbo.CLIENTE DROP COLUMN acepta_promociones;
IF COL_LENGTH('dbo.PEDIDO', 'id_reserva') IS NOT NULL ALTER TABLE dbo.PEDIDO DROP COLUMN id_reserva;
IF COL_LENGTH('dbo.RESERVA_MESA', 'id_cliente') IS NOT NULL ALTER TABLE dbo.RESERVA_MESA DROP COLUMN id_cliente;

COMMIT TRANSACTION;
GO
