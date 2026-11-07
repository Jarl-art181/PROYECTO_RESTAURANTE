using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // GESTIÓN DE REPORTES (FASE G) - CONSULTAS DE SOLO LECTURA
    // ----------------------------------------------------------
    // Reglas:
    //  * VENTA = pedido en estado 'cobrado'. Los pendientes,
    //    entregados sin cobrar o cancelados nunca suman ventas.
    //  * Fecha de la venta = fecha del ÚLTIMO pago del pedido
    //    (cuando quedó cobrado).
    //  * Importe de la venta = V_PEDIDO_TOTAL.total (incluye
    //    descuentos y propina), igual a la suma de sus pagos.
    //  * Ingresos por método = cada fila de PAGO una sola vez:
    //    un pago mixto aporta a cada método solo su parte, y la
    //    suma de métodos es igual al total (sin duplicar).
    //  * Métodos se agrupan sin distinguir mayúsculas.
    //  * Solo SELECT parametrizados: no escribe en la base.
    // ==========================================================
    internal sealed class FiltroReporte
    {
        public DateTime Desde;            // inclusive (fecha)
        public DateTime Hasta;            // inclusive (fecha)
        public string Canal;              // null = todos; salon / delivery / recojo
        public string Metodo;             // null = todos; efectivo / yape / plin / tarjeta / transferencia
        public string Estado;             // null = todos (solo para "pedidos por estado")

        public DateTime HastaExclusivo { get { return Hasta.Date.AddDays(1); } }

        public string Descripcion()
        {
            string t = Desde.Date == Hasta.Date
                ? Desde.ToString("dd/MM/yyyy")
                : Desde.ToString("dd/MM/yyyy") + " al " + Hasta.ToString("dd/MM/yyyy");
            if (Canal != null) t += " · canal " + ReporteDatos.NombreCanal(Canal);
            if (Metodo != null) t += " · " + ReporteDatos.NombreMetodo(Metodo);
            if (Estado != null) t += " · estado " + Estado;
            return t;
        }
    }

    internal sealed class ResumenReporte
    {
        public decimal Ventas, Propinas, Descuentos, TicketPromedio;
        public int PedidosCobrados, PagosMixtos, PedidosPendientes, Reservas;
    }

    internal static class ReporteDatos
    {
        public static readonly string[] Metodos = { "efectivo", "yape", "plin", "tarjeta", "transferencia" };
        public static readonly string[] Canales = { "salon", "delivery", "recojo" };

        public static string NombreMetodo(string m)
        {
            switch ((m ?? "").ToLowerInvariant())
            {
                case "efectivo": return "Efectivo";
                case "yape": return "Yape";
                case "plin": return "Plin";
                case "tarjeta": return "Tarjeta";
                case "transferencia": return "Transferencia";
                default: return string.IsNullOrEmpty(m) ? "-" : CultureInfo.GetCultureInfo("es-PE").TextInfo.ToTitleCase(m.ToLowerInvariant());
            }
        }

        public static string NombreCanal(string c)
        {
            switch ((c ?? "").ToLowerInvariant())
            {
                case "salon": return "Salón";
                case "delivery": return "Delivery";
                case "recojo": return "Recojo";
                default: return c ?? "-";
            }
        }

        // Pedidos cobrados en el período (y filtros de canal / método).
        private const string CteVentas = @"
            WITH Cobro AS (
                SELECT id_pedido, MAX(fecha_hora) AS fecha_cobro
                FROM dbo.PAGO GROUP BY id_pedido
            ),
            Ventas AS (
                SELECT P.id_pedido, P.canal, P.id_promocion, P.id_cliente, P.id_mesero,
                       ISNULL(P.propina, 0) AS propina, ISNULL(P.descuento_aplicado, 0) AS descuento_pedido,
                       V.total, C.fecha_cobro
                FROM dbo.PEDIDO P
                INNER JOIN Cobro C ON C.id_pedido = P.id_pedido
                INNER JOIN dbo.V_PEDIDO_TOTAL V ON V.id_pedido = P.id_pedido
                WHERE P.estado = 'cobrado'
                  AND C.fecha_cobro >= @Desde AND C.fecha_cobro < @Hasta
                  AND (@Canal IS NULL OR P.canal = @Canal)
                  AND (@Metodo IS NULL OR EXISTS (
                        SELECT 1 FROM dbo.PAGO X WHERE X.id_pedido = P.id_pedido AND LOWER(X.[método]) = @Metodo))
            )";

        private static DataTable Consultar(string sql, FiltroReporte f)
        {
            DataTable t = new DataTable();
            using (SqlConnection cx = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, cx))
            {
                cmd.CommandTimeout = 30;
                cmd.Parameters.Add("@Desde", SqlDbType.DateTime2).Value = f.Desde.Date;
                cmd.Parameters.Add("@Hasta", SqlDbType.DateTime2).Value = f.HastaExclusivo;
                cmd.Parameters.Add("@Canal", SqlDbType.VarChar, 20).Value = (object)f.Canal ?? DBNull.Value;
                cmd.Parameters.Add("@Metodo", SqlDbType.VarChar, 30).Value = (object)f.Metodo ?? DBNull.Value;
                cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = (object)f.Estado ?? DBNull.Value;
                using (SqlDataAdapter a = new SqlDataAdapter(cmd))
                    a.Fill(t);
            }
            return t;
        }

        // ------------------------------------------------------
        // INDICADORES
        // ------------------------------------------------------
        public static ResumenReporte Resumen(FiltroReporte f)
        {
            DataTable t = Consultar(CteVentas + @"
                SELECT
                    (SELECT ISNULL(SUM(total), 0) FROM Ventas) AS ventas,
                    (SELECT COUNT(*) FROM Ventas) AS pedidos,
                    (SELECT ISNULL(SUM(propina), 0) FROM Ventas) AS propinas,
                    ISNULL((SELECT SUM(D.cantidad * D.descuento) FROM dbo.DETALLE_PEDIDO_PLATO D WHERE D.id_pedido IN (SELECT id_pedido FROM Ventas)), 0)
                  + ISNULL((SELECT SUM(D.cantidad * D.descuento) FROM dbo.DETALLE_PEDIDO_COMBO D WHERE D.id_pedido IN (SELECT id_pedido FROM Ventas)), 0)
                  + (SELECT ISNULL(SUM(descuento_pedido), 0) FROM Ventas) AS descuentos,
                    (SELECT COUNT(*) FROM Ventas V2
                      WHERE (SELECT COUNT(DISTINCT LOWER([método])) FROM dbo.PAGO X WHERE X.id_pedido = V2.id_pedido) > 1) AS mixtos,
                    (SELECT COUNT(*) FROM dbo.PEDIDO P WHERE P.estado IN ('pendiente','en_preparacion','listo','entregado')
                        AND P.fecha_hora >= @Desde AND P.fecha_hora < @Hasta AND (@Canal IS NULL OR P.canal = @Canal)) AS pendientes,
                    (SELECT COUNT(*) FROM dbo.RESERVA_MESA R WHERE R.fecha_hora >= @Desde AND R.fecha_hora < @Hasta) AS reservas;", f);
            DataRow r = t.Rows[0];
            ResumenReporte s = new ResumenReporte
            {
                Ventas = Convert.ToDecimal(r["ventas"]),
                PedidosCobrados = Convert.ToInt32(r["pedidos"]),
                Propinas = Convert.ToDecimal(r["propinas"]),
                Descuentos = Convert.ToDecimal(r["descuentos"]),
                PagosMixtos = Convert.ToInt32(r["mixtos"]),
                PedidosPendientes = Convert.ToInt32(r["pendientes"]),
                Reservas = Convert.ToInt32(r["reservas"])
            };
            s.TicketPromedio = s.PedidosCobrados == 0 ? 0 : Math.Round(s.Ventas / s.PedidosCobrados, 2, MidpointRounding.AwayFromZero);
            return s;
        }

        // ------------------------------------------------------
        // REPORTES (cada uno devuelve una tabla lista para mostrar)
        // ------------------------------------------------------

        // Ventas por día / semana / mes.
        public static DataTable VentasPorPeriodo(FiltroReporte f, string agrupacion)
        {
            string clave, etiqueta;
            switch (agrupacion)
            {
                case "semana":
                    clave = "DATEADD(DAY, -((DATEDIFF(DAY, '19000101', fecha_cobro)) % 7), CONVERT(date, fecha_cobro))";
                    etiqueta = "'Semana del ' + CONVERT(varchar(10), {0}, 103)";
                    break;
                case "mes":
                    clave = "DATEFROMPARTS(YEAR(fecha_cobro), MONTH(fecha_cobro), 1)";
                    etiqueta = "RIGHT('0' + CAST(MONTH({0}) AS varchar(2)), 2) + '/' + CAST(YEAR({0}) AS varchar(4))";
                    break;
                default:
                    clave = "CONVERT(date, fecha_cobro)";
                    etiqueta = "CONVERT(varchar(10), {0}, 103)";
                    break;
            }
            return Consultar(CteVentas + @"
                SELECT " + string.Format(etiqueta, "G.periodo") + @" AS [Período],
                       G.pedidos AS [Pedidos cobrados], G.ventas AS [Ventas (S/)],
                       G.propinas AS [Propinas (S/)],
                       CAST(G.ventas / NULLIF(G.pedidos, 0) AS decimal(10,2)) AS [Ticket promedio (S/)]
                FROM (SELECT " + clave + @" AS periodo, COUNT(*) AS pedidos, SUM(total) AS ventas, SUM(propina) AS propinas
                      FROM Ventas GROUP BY " + clave + @") G
                ORDER BY G.periodo;", f);
        }

        public static DataTable VentasPorCanal(FiltroReporte f)
        {
            return Consultar(CteVentas + @"
                SELECT CASE canal WHEN 'salon' THEN 'Salón' WHEN 'delivery' THEN 'Delivery' WHEN 'recojo' THEN 'Recojo' ELSE canal END AS [Canal],
                       COUNT(*) AS [Pedidos cobrados], SUM(total) AS [Ventas (S/)],
                       CAST(100.0 * SUM(total) / NULLIF((SELECT SUM(total) FROM Ventas), 0) AS decimal(5,1)) AS [% de ventas]
                FROM Ventas GROUP BY canal ORDER BY SUM(total) DESC;", f);
        }

        // Cada pago una sola vez: un pago mixto reparte su importe entre métodos.
        public static DataTable IngresosPorMetodo(FiltroReporte f)
        {
            return Consultar(CteVentas + @"
                SELECT LOWER(X.[método]) AS metodo, COUNT(*) AS [Pagos], COUNT(DISTINCT X.id_pedido) AS [Pedidos],
                       SUM(X.importe) AS [Ingresos (S/)],
                       CAST(100.0 * SUM(X.importe) / NULLIF((SELECT SUM(P2.importe) FROM dbo.PAGO P2
                            WHERE P2.id_pedido IN (SELECT id_pedido FROM Ventas)
                              AND (@Metodo IS NULL OR LOWER(P2.[método]) = @Metodo)), 0) AS decimal(5,1)) AS [% de ingresos]
                FROM dbo.PAGO X
                WHERE X.id_pedido IN (SELECT id_pedido FROM Ventas)
                  AND (@Metodo IS NULL OR LOWER(X.[método]) = @Metodo)
                GROUP BY LOWER(X.[método])
                ORDER BY SUM(X.importe) DESC;", f);
        }

        public static DataTable PagosMixtos(FiltroReporte f)
        {
            return Consultar(CteVentas + @"
                SELECT V.id_pedido AS [Pedido], CONVERT(varchar(16), V.fecha_cobro, 120) AS [Cobrado],
                       STUFF((SELECT ' + ' + LOWER(X.[método]) + ' S/ ' + CAST(X.importe AS varchar(20))
                              FROM dbo.PAGO X WHERE X.id_pedido = V.id_pedido ORDER BY X.id_pago
                              FOR XML PATH('')), 1, 3, '') AS [Detalle de pagos],
                       V.total AS [Total (S/)]
                FROM Ventas V
                WHERE (SELECT COUNT(DISTINCT LOWER([método])) FROM dbo.PAGO X WHERE X.id_pedido = V.id_pedido) > 1
                ORDER BY V.fecha_cobro;", f);
        }

        // Todos los estados (no solo ventas), por fecha de registro del pedido.
        public static DataTable PedidosPorEstado(FiltroReporte f)
        {
            return Consultar(@"
                SELECT CASE P.estado WHEN 'pendiente' THEN 'Pendiente' WHEN 'en_preparacion' THEN 'En preparación'
                            WHEN 'listo' THEN 'Listo' WHEN 'entregado' THEN 'Entregado (sin cobrar)'
                            WHEN 'cobrado' THEN 'Cobrado' WHEN 'cancelado' THEN 'Cancelado' ELSE P.estado END AS [Estado],
                       COUNT(*) AS [Pedidos],
                       SUM(V.total) AS [Importe (S/)],
                       CASE WHEN P.estado = 'cobrado' THEN 'Venta' ELSE 'No es venta' END AS [Cuenta como venta]
                FROM dbo.PEDIDO P
                INNER JOIN dbo.V_PEDIDO_TOTAL V ON V.id_pedido = P.id_pedido
                WHERE P.fecha_hora >= @Desde AND P.fecha_hora < @Hasta
                  AND (@Canal IS NULL OR P.canal = @Canal)
                  AND (@Estado IS NULL OR P.estado = @Estado)
                GROUP BY P.estado
                ORDER BY COUNT(*) DESC;", f);
        }

        public static DataTable ProductosMasVendidos(FiltroReporte f)
        {
            return Consultar(CteVentas + @"
                SELECT TOP (20) X.nombre AS [Producto], X.tipo AS [Tipo],
                       SUM(X.cantidad) AS [Unidades], SUM(X.importe) AS [Importe (S/)],
                       COUNT(DISTINCT X.id_pedido) AS [Pedidos]
                FROM (
                    SELECT D.id_pedido, PL.nombre, 'Plato/bebida' AS tipo, D.cantidad,
                           D.cantidad * (D.precio_unitario - D.descuento) AS importe
                    FROM dbo.DETALLE_PEDIDO_PLATO D INNER JOIN dbo.PLATO PL ON PL.id_plato = D.id_plato
                    WHERE D.id_pedido IN (SELECT id_pedido FROM Ventas)
                    UNION ALL
                    SELECT D.id_pedido, C.nombre, 'Combo', D.cantidad,
                           D.cantidad * (D.precio_unitario - D.descuento)
                    FROM dbo.DETALLE_PEDIDO_COMBO D INNER JOIN dbo.COMBO C ON C.id_combo = D.id_combo
                    WHERE D.id_pedido IN (SELECT id_pedido FROM Ventas)
                ) X
                GROUP BY X.nombre, X.tipo
                ORDER BY SUM(X.cantidad) DESC, SUM(X.importe) DESC;", f);
        }

        // Cobros registrados por cada empleado autorizado (cajero del pago).
        public static DataTable CobrosPorEmpleado(FiltroReporte f)
        {
            return Consultar(CteVentas + @"
                SELECT LTRIM(RTRIM(E.nombre + ' ' + E.apellidos)) AS [Empleado],
                       COUNT(DISTINCT X.id_pedido) AS [Pedidos cobrados], COUNT(*) AS [Pagos registrados],
                       SUM(X.importe) AS [Ingresos cobrados (S/)]
                FROM dbo.PAGO X
                INNER JOIN dbo.EMPLEADO E ON E.id_empleado = X.id_cajero
                WHERE X.id_pedido IN (SELECT id_pedido FROM Ventas)
                  AND (@Metodo IS NULL OR LOWER(X.[método]) = @Metodo)
                GROUP BY E.id_empleado, E.nombre, E.apellidos
                ORDER BY SUM(X.importe) DESC;", f);
        }

        public static DataTable Promociones(FiltroReporte f)
        {
            return Consultar(CteVentas + @"
                SELECT ISNULL(PR.nombre, 'Sin promoción') AS [Promoción],
                       COUNT(*) AS [Pedidos],
                       SUM(ISNULL(D.descuento_lineas, 0) + V.descuento_pedido) AS [Descuento otorgado (S/)],
                       SUM(V.total) AS [Ventas (S/)]
                FROM Ventas V
                LEFT JOIN dbo.PROMOCION PR ON PR.id_promocion = V.id_promocion
                OUTER APPLY (
                    SELECT ISNULL((SELECT SUM(cantidad * descuento) FROM dbo.DETALLE_PEDIDO_PLATO WHERE id_pedido = V.id_pedido), 0)
                         + ISNULL((SELECT SUM(cantidad * descuento) FROM dbo.DETALLE_PEDIDO_COMBO WHERE id_pedido = V.id_pedido), 0) AS descuento_lineas
                ) D
                GROUP BY PR.nombre
                ORDER BY CASE WHEN PR.nombre IS NULL THEN 1 ELSE 0 END, COUNT(*) DESC;", f);
        }

        // Clientes con compras cobradas en el período (frecuentes primero).
        public static DataTable ClientesFrecuentes(FiltroReporte f)
        {
            return Consultar(CteVentas + @"
                SELECT LTRIM(RTRIM(C.nombre + ' ' + C.apellidos)) AS [Cliente], C.documento AS [Documento],
                       COUNT(*) AS [Compras], SUM(V.total) AS [Total comprado (S/)],
                       CONVERT(varchar(10), MAX(V.fecha_cobro), 103) AS [Última compra],
                       ISNULL(S.saldo_puntos, 0) AS [Puntos],
                       (SELECT COUNT(*) FROM dbo.PEDIDO P WHERE P.id_cliente = C.id_cliente AND P.estado = 'cobrado') AS [Compras históricas]
                FROM Ventas V
                INNER JOIN dbo.CLIENTE C ON C.id_cliente = V.id_cliente
                LEFT JOIN dbo.V_CLIENTE_SALDO S ON S.id_cliente = C.id_cliente
                GROUP BY C.id_cliente, C.nombre, C.apellidos, C.documento, S.saldo_puntos
                ORDER BY COUNT(*) DESC, SUM(V.total) DESC;", f);
        }

        // Historial de compras de un cliente (todas las fechas).
        public static DataTable HistorialCliente(int idCliente)
        {
            DataTable t = new DataTable();
            using (SqlConnection cx = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT P.id_pedido AS [Pedido], CONVERT(varchar(16), P.fecha_hora, 120) AS [Fecha],
                       CASE P.canal WHEN 'salon' THEN 'Salón' WHEN 'delivery' THEN 'Delivery' ELSE 'Recojo' END AS [Canal],
                       P.estado AS [Estado], V.total AS [Total (S/)]
                FROM dbo.PEDIDO P INNER JOIN dbo.V_PEDIDO_TOTAL V ON V.id_pedido = P.id_pedido
                WHERE P.id_cliente = @Cliente
                ORDER BY P.fecha_hora DESC;", cx))
            {
                cmd.Parameters.Add("@Cliente", SqlDbType.Int).Value = idCliente;
                using (SqlDataAdapter a = new SqlDataAdapter(cmd)) a.Fill(t);
            }
            return t;
        }

        // Reservas realizadas, atendidas (llegada confirmada) y canceladas/vencidas.
        public static DataTable Reservas(FiltroReporte f)
        {
            return Consultar(@"
                SELECT CASE R.estado WHEN 'confirmada' THEN 'Por atender (confirmada)'
                                     WHEN 'finalizada' THEN 'Atendida (llegó el cliente)'
                                     WHEN 'cancelada' THEN 'Cancelada o vencida' ELSE R.estado END AS [Estado],
                       COUNT(*) AS [Reservas], SUM(R.personas) AS [Personas],
                       SUM(CASE WHEN PR.id_reserva IS NOT NULL THEN 1 ELSE 0 END) AS [Con pedido],
                       SUM(CASE WHEN R.id_cliente IS NOT NULL THEN 1 ELSE 0 END) AS [De clientes registrados]
                FROM dbo.RESERVA_MESA R
                LEFT JOIN (SELECT DISTINCT id_reserva FROM dbo.PEDIDO WHERE id_reserva IS NOT NULL) PR ON PR.id_reserva = R.id_reserva
                WHERE R.fecha_hora >= @Desde AND R.fecha_hora < @Hasta
                GROUP BY R.estado
                ORDER BY COUNT(*) DESC;", f);
        }
    }
}
