/* =====================================================================
   FASE 2A · Script 02 — Verificación de la migración (NO deja cambios)
   Todas las pruebas se ejecutan en una transacción que se REVIERTE.

     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -d AL_ESTILO_PIURA -I -f 65001
            -i FASE2A_02_verificar.sql
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT OFF;   -- permite capturar cada error y seguir probando
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @R TABLE (n INT IDENTITY, prueba NVARCHAR(200), esperado NVARCHAR(20), resultado NVARCHAR(20), detalle NVARCHAR(400));
DECLARE @id1 INT, @id2 INT, @id3 INT, @turno VARCHAR(20);
DECLARE @PedidosAntes INT = (SELECT COUNT(*) FROM dbo.PEDIDO),
        @PagosAntes   INT = (SELECT COUNT(*) FROM dbo.PAGO);

BEGIN TRANSACTION;

/* 1. Empleado sin cuenta */
BEGIN TRY
    INSERT INTO dbo.EMPLEADO (dni, nombre, apellidos, [teléfono], usuario, [contraseña], estado, id_sucursal, turno)
    VALUES ('90000001', 'Prueba', 'Sin Cuenta', NULL, NULL, NULL, 'activo', 1, 'tarde');
    SET @id1 = SCOPE_IDENTITY();
    INSERT INTO @R VALUES ('Registrar empleado sin usuario ni contraseña', 'acepta', 'acepta', NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Registrar empleado sin usuario ni contraseña', 'acepta', 'rechaza', ERROR_MESSAGE()); END CATCH;

/* 2. Segundo empleado sin cuenta (varios NULL en usuario) */
BEGIN TRY
    INSERT INTO dbo.EMPLEADO (dni, nombre, apellidos, usuario, [contraseña], estado, id_sucursal)
    VALUES ('90000002', 'Prueba', 'Sin Cuenta Dos', NULL, NULL, 'activo', 1);
    SET @id2 = SCOPE_IDENTITY();
    INSERT INTO @R VALUES ('Dos empleados sin usuario a la vez', 'acepta', 'acepta', NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Dos empleados sin usuario a la vez', 'acepta', 'rechaza', ERROR_MESSAGE()); END CATCH;

/* 3. Usuario duplicado */
BEGIN TRY
    INSERT INTO dbo.EMPLEADO (dni, nombre, apellidos, usuario, [contraseña], estado, id_sucursal, acceso_habilitado)
    VALUES ('90000003', 'Prueba', 'Duplicado', 'lquispe', 'x', 'activo', 1, 1);
    INSERT INTO @R VALUES ('Usuario repetido (lquispe)', 'rechaza', 'acepta', NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Usuario repetido (lquispe)', 'rechaza', 'rechaza', ERROR_MESSAGE()); END CATCH;

/* 4. DNI duplicado */
BEGIN TRY
    INSERT INTO dbo.EMPLEADO (dni, nombre, apellidos, estado, id_sucursal)
    VALUES ('90000001', 'Prueba', 'DNI Repetido', 'activo', 1);
    INSERT INTO @R VALUES ('DNI repetido', 'rechaza', 'acepta', NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('DNI repetido', 'rechaza', 'rechaza', ERROR_MESSAGE()); END CATCH;

/* 5. Cuenta habilitada sin credenciales */
BEGIN TRY
    UPDATE dbo.EMPLEADO SET acceso_habilitado = 1 WHERE id_empleado = @id1;
    INSERT INTO @R VALUES ('Habilitar cuenta sin usuario/contraseña', 'rechaza', 'acepta', NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Habilitar cuenta sin usuario/contraseña', 'rechaza', 'rechaza', ERROR_MESSAGE()); END CATCH;

/* 6. Turno inválido */
BEGIN TRY
    UPDATE dbo.EMPLEADO SET turno = 'madrugada' WHERE id_empleado = @id1;
    INSERT INTO @R VALUES ('Turno no permitido (madrugada)', 'rechaza', 'acepta', NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Turno no permitido (madrugada)', 'rechaza', 'rechaza', ERROR_MESSAGE()); END CATCH;

/* 7. Estado laboral y cuenta son independientes */
BEGIN TRY
    UPDATE dbo.EMPLEADO SET estado = 'inactivo' WHERE id_empleado = @id2;
    IF EXISTS (SELECT 1 FROM dbo.EMPLEADO WHERE id_empleado = @id2 AND estado = 'inactivo' AND acceso_habilitado = 0)
        INSERT INTO @R VALUES ('Desactivar empleado sin tocar la cuenta', 'acepta', 'acepta', NULL);
    ELSE
        INSERT INTO @R VALUES ('Desactivar empleado sin tocar la cuenta', 'acepta', 'error', 'estado inesperado');
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Desactivar empleado sin tocar la cuenta', 'acepta', 'rechaza', ERROR_MESSAGE()); END CATCH;

/* 8. Nuevo cajero toma el turno de EMPLEADO */
BEGIN TRY
    INSERT INTO dbo.CAJERO (id_empleado, turno) VALUES (@id1, NULL);
    SELECT @turno = turno FROM dbo.CAJERO WHERE id_empleado = @id1;
    INSERT INTO @R VALUES ('Cajero nuevo hereda EMPLEADO.turno (tarde)', 'tarde', ISNULL(@turno, 'NULL'), NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Cajero nuevo hereda EMPLEADO.turno (tarde)', 'tarde', 'error', ERROR_MESSAGE()); END CATCH;

/* 9. Cambiar EMPLEADO.turno actualiza CAJERO.turno */
BEGIN TRY
    UPDATE dbo.EMPLEADO SET turno = 'noche' WHERE id_empleado = 3;
    SELECT @turno = turno FROM dbo.CAJERO WHERE id_empleado = 3;
    INSERT INTO @R VALUES ('Cambio de turno del cajero 3 se copia a CAJERO', 'noche', ISNULL(@turno, 'NULL'), NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Cambio de turno del cajero 3 se copia a CAJERO', 'noche', 'error', ERROR_MESSAGE()); END CATCH;

/* 10. Editar CAJERO.turno directamente se corrige desde EMPLEADO */
BEGIN TRY
    UPDATE dbo.CAJERO SET turno = 'tarde' WHERE id_empleado = 3;
    SELECT @turno = turno FROM dbo.CAJERO WHERE id_empleado = 3;
    INSERT INTO @R VALUES ('CAJERO.turno no puede divergir de EMPLEADO.turno', 'noche', ISNULL(@turno, 'NULL'), NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('CAJERO.turno no puede divergir de EMPLEADO.turno', 'noche', 'error', ERROR_MESSAGE()); END CATCH;

/* 11. Historial intacto */
INSERT INTO @R
SELECT 'Pedidos y pagos sin cambios', CAST(@PedidosAntes AS VARCHAR) + '/' + CAST(@PagosAntes AS VARCHAR),
       CAST((SELECT COUNT(*) FROM dbo.PEDIDO) AS VARCHAR) + '/' + CAST((SELECT COUNT(*) FROM dbo.PAGO) AS VARCHAR), NULL;

/* 12. ÚLTIMA PRUEBA: la regla ISA (un solo cargo) sigue activa.
   Su trigger hace ROLLBACK de toda la transacción: por eso va al final. */
BEGIN TRY
    INSERT INTO dbo.MESERO (id_empleado) VALUES (@id1);   -- @id1 ya es CAJERO
    INSERT INTO @R VALUES ('Regla ISA: un empleado con dos cargos', 'rechaza', 'acepta', NULL);
END TRY
BEGIN CATCH INSERT INTO @R VALUES ('Regla ISA: un empleado con dos cargos', 'rechaza', 'rechaza', ERROR_MESSAGE()); END CATCH;

IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

SELECT n, prueba, esperado, resultado,
       CASE WHEN esperado = resultado THEN 'OK' ELSE 'FALLA' END AS veredicto,
       LEFT(detalle, 110) AS detalle
  FROM @R ORDER BY n;

/* Confirmar que no quedó ningún dato de prueba */
SELECT COUNT(*) AS empleados_de_prueba_restantes FROM dbo.EMPLEADO WHERE dni LIKE '9000000_';
SELECT turno AS turno_cajero_3_tras_reversion FROM dbo.CAJERO WHERE id_empleado = 3;
