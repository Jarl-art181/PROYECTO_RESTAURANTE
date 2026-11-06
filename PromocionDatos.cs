using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace PROYECTO_RESTAURANTE
{
    internal sealed class PromocionInfo
    {
        public int IdPromocion;
        public string Nombre;
        public string TipoDescuento = "porcentaje";   // porcentaje / monto
        public decimal Valor;
        public string Alcance = "platos";             // platos / pedido / general
        public DateTime FechaInicio;
        public DateTime FechaFin;
        public string Estado = "activo";
        public string Descripcion = "";
        public string DiasSemana;                     // ISO: 1 = lunes ... 7 = domingo; null = todos
        public decimal? MontoMinimo;
        public List<int> Platos = new List<int>();
        public List<string> NombresPlatos = new List<string>();

        // Solo lectura: uso histórico.
        public int PedidosConPromocion;
        public decimal DescuentoOtorgado;            // pedidos cobrados

        public bool Activa { get { return Estado == "activo"; } }
        public bool PorPlatos { get { return Alcance == "platos"; } }

        public string DescuentoTexto
        {
            get
            {
                return TipoDescuento == "porcentaje"
                    ? Valor.ToString("0.##", CultureInfo.InvariantCulture) + " %"
                    : "S/ " + Valor.ToString("0.00", CultureInfo.InvariantCulture);
            }
        }

        public string AlcanceTexto
        {
            get { return PorPlatos ? "Platos seleccionados" : Alcance == "general" ? "Todo el pedido (general)" : "Todo el pedido"; }
        }

        public string DiasTexto { get { return PromocionDatos.DiasTexto(DiasSemana); } }

        public string Situacion(DateTime hoy)
        {
            hoy = hoy.Date;
            if (!Activa) return "Inactiva";
            if (hoy < FechaInicio) return "Programada";
            if (hoy > FechaFin) return "Vencida";
            return PromocionDatos.DiaPermitido(DiasSemana, hoy) ? "Vigente hoy" : "Vigente (otro día)";
        }
    }

    // Línea de pedido para calcular descuentos (precio BASE, sin descuento).
    internal sealed class LineaDescuento
    {
        public string Tipo;         // plato / combo
        public int IdProducto;
        public decimal Cantidad;
        public decimal PrecioUnitario;
        public decimal DescuentoUnitario;   // resultado

        public LineaDescuento() { }

        public LineaDescuento(string tipo, int idProducto, decimal cantidad, decimal precioUnitario)
        {
            Tipo = tipo;
            IdProducto = idProducto;
            Cantidad = cantidad;
            PrecioUnitario = precioUnitario;
        }
    }

    internal sealed class ResultadoPromocion
    {
        public bool Aplicable;
        public string Motivo;                 // por qué no aplica
        public decimal Subtotal;              // sin descuentos ni propina
        public decimal DescuentoLineas;       // suma cantidad * descuento unitario
        public decimal DescuentoPedido;       // va a PEDIDO.descuento_aplicado
        public decimal DescuentoTotal { get { return DescuentoLineas + DescuentoPedido; } }
    }

    // ==========================================================
    // PROMOCIONES (FASE F)
    // ----------------------------------------------------------
    // Reglas aprobadas:
    //  * Una promoción por pedido (PEDIDO.id_promocion), elegida a
    //    mano por un usuario autorizado.
    //  * Aplica si: activa, hoy entre inicio y fin, día ISO
    //    permitido, subtotal >= monto mínimo y, si es por platos,
    //    al menos un plato elegible en el pedido.
    //  * Por platos: descuento POR UNIDAD solo en los platos
    //    participantes (DETALLE_PEDIDO_PLATO.descuento).
    //    Porcentaje = precio x valor / 100 (2 decimales);
    //    monto fijo = mínimo(valor, precio).
    //  * Todo el pedido: PEDIDO.descuento_aplicado sobre el
    //    subtotal (sin propina), nunca mayor que el subtotal.
    //  * Siempre se calcula desde el precio base: recalcular no
    //    descuenta dos veces. Nunca da importes negativos.
    //  * Días: ISO 8601 (1 = lunes ... 7 = domingo), sin
    //    depender de DATEFIRST ni de la configuración regional.
    // ==========================================================
    internal static class PromocionDatos
    {
        private static readonly string[] NombresDia = { "", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };

        public static int DiaIso(DateTime fecha)
        {
            // DayOfWeek: domingo = 0 ... sábado = 6 (no depende de la cultura).
            int d = (int)fecha.DayOfWeek;
            return d == 0 ? 7 : d;
        }

        public static bool DiaPermitido(string dias, DateTime fecha)
        {
            return string.IsNullOrEmpty(dias) || dias.IndexOf((char)('0' + DiaIso(fecha))) >= 0;
        }

        public static string DiasTexto(string dias)
        {
            if (string.IsNullOrEmpty(dias) || dias.Length == 7) return "Todos los días";
            return string.Join(", ", dias.Select(c => NombresDia[c - '0']));
        }

        // Ordena y quita repetidos; null = todos los días.
        public static string NormalizarDias(IEnumerable<int> dias)
        {
            List<int> lista = dias.Where(d => d >= 1 && d <= 7).Distinct().OrderBy(d => d).ToList();
            if (lista.Count == 0)
                throw new ReglaNegocioException("Marque al menos un día de la semana.");
            return lista.Count == 7 ? null : string.Concat(lista);
        }

        public static bool EsquemaListo()
        {
            using (SqlConnection cx = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(
                "SELECT CASE WHEN COL_LENGTH('dbo.PROMOCION','dias_semana') IS NOT NULL AND COL_LENGTH('dbo.PROMOCION','monto_minimo') IS NOT NULL " +
                "AND COL_LENGTH('dbo.PROMOCION','descripcion') IS NOT NULL THEN 1 ELSE 0 END;", cx))
            {
                cx.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        // ------------------------------------------------------
        // CONSULTAS
        // ------------------------------------------------------
        public static List<PromocionInfo> Listar()
        {
            if (!EsquemaListo())
                throw new ReglaNegocioException("La base de datos aún no tiene la actualización de Promociones " +
                    "(SQL\\FASEF_01_promociones.sql). Consulte con el Administrador.");

            List<PromocionInfo> lista = new List<PromocionInfo>();
            Dictionary<int, PromocionInfo> porId = new Dictionary<int, PromocionInfo>();
            using (SqlConnection cx = new SqlConnection(BaseDatos.CadenaConexion))
            {
                cx.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT R.id_promocion, R.nombre, R.tipo_descuento, R.valor, R.alcance, R.fecha_inicio, R.fecha_fin,
                           R.estado, R.descripcion, R.dias_semana, R.monto_minimo,
                           (SELECT COUNT(*) FROM dbo.PEDIDO P WHERE P.id_promocion = R.id_promocion AND P.estado <> 'cancelado') AS pedidos,
                           (SELECT ISNULL(SUM(ISNULL(P.descuento_aplicado, 0) + ISNULL(DP.d, 0) + ISNULL(DC.d, 0)), 0)
                            FROM dbo.PEDIDO P
                            OUTER APPLY (SELECT SUM(D.cantidad * D.descuento) AS d FROM dbo.DETALLE_PEDIDO_PLATO D WHERE D.id_pedido = P.id_pedido) DP
                            OUTER APPLY (SELECT SUM(C.cantidad * C.descuento) AS d FROM dbo.DETALLE_PEDIDO_COMBO C WHERE C.id_pedido = P.id_pedido) DC
                            WHERE P.id_promocion = R.id_promocion AND P.estado = 'cobrado') AS otorgado
                    FROM dbo.PROMOCION R
                    ORDER BY R.fecha_fin DESC, R.nombre;", cx))
                {
                    cmd.CommandTimeout = 20;
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            PromocionInfo p = new PromocionInfo
                            {
                                IdPromocion = r.GetInt32(0),
                                Nombre = r.GetString(1),
                                TipoDescuento = r.GetString(2).Trim().ToLowerInvariant(),
                                Valor = r.GetDecimal(3),
                                Alcance = r.GetString(4).Trim().ToLowerInvariant(),
                                FechaInicio = r.GetDateTime(5),
                                FechaFin = r.GetDateTime(6),
                                Estado = r.GetString(7).Trim().ToLowerInvariant(),
                                Descripcion = r.IsDBNull(8) ? "" : r.GetString(8),
                                DiasSemana = r.IsDBNull(9) ? null : r.GetString(9),
                                MontoMinimo = r.IsDBNull(10) ? (decimal?)null : r.GetDecimal(10),
                                PedidosConPromocion = r.GetInt32(11),
                                DescuentoOtorgado = r.GetDecimal(12)
                            };
                            lista.Add(p);
                            porId[p.IdPromocion] = p;
                        }
                    }
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT PP.id_promocion, PP.id_plato, P.nombre
                    FROM dbo.PROMOCION_PLATO PP JOIN dbo.PLATO P ON P.id_plato = PP.id_plato
                    ORDER BY P.nombre;", cx))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        PromocionInfo p;
                        if (porId.TryGetValue(r.GetInt32(0), out p))
                        {
                            p.Platos.Add(r.GetInt32(1));
                            p.NombresPlatos.Add(r.GetString(2));
                        }
                    }
                }
            }
            return lista;
        }

        // Promociones por plato vigentes en la fecha: id_plato -> texto de la etiqueta.
        // Si la base no tiene la migración, no hay etiquetas (no es un error).
        public static Dictionary<int, string> EtiquetasVigentes(DateTime fecha)
        {
            Dictionary<int, string> r = new Dictionary<int, string>();
            if (!EsquemaListo())
                return r;

            foreach (PromocionInfo p in Listar().Where(x => x.PorPlatos && x.Situacion(fecha) == "Vigente hoy"))
            {
                foreach (int id in p.Platos)
                {
                    if (!r.ContainsKey(id))
                        r[id] = "PROMO -" + p.DescuentoTexto.Replace(" ", "");
                }
            }
            return r;
        }

        // ------------------------------------------------------
        // VALIDACIÓN
        // ------------------------------------------------------
        public static void Validar(PromocionInfo p)
        {
            p.Nombre = (p.Nombre ?? "").Trim();
            p.Descripcion = (p.Descripcion ?? "").Trim();
            while (p.Nombre.Contains("  ")) p.Nombre = p.Nombre.Replace("  ", " ");

            if (p.Nombre.Length < 3 || p.Nombre.Length > 100)
                throw new ReglaNegocioException("El nombre de la promoción debe tener entre 3 y 100 caracteres.");
            if (p.Descripcion.Length > 255)
                throw new ReglaNegocioException("La descripción no puede superar 255 caracteres.");
            if (p.TipoDescuento != "porcentaje" && p.TipoDescuento != "monto")
                throw new ReglaNegocioException("Seleccione el tipo de descuento.");
            if (p.Valor <= 0 || decimal.Round(p.Valor, 2) != p.Valor)
                throw new ReglaNegocioException("El descuento debe ser mayor que 0, con hasta 2 decimales.");
            if (p.TipoDescuento == "porcentaje" && p.Valor > 100)
                throw new ReglaNegocioException("El porcentaje no puede superar 100 %.");
            if (p.TipoDescuento == "monto" && p.Valor > 9999.99m)
                throw new ReglaNegocioException("El monto de descuento es demasiado alto.");
            if (p.Alcance != "platos" && p.Alcance != "pedido" && p.Alcance != "general")
                throw new ReglaNegocioException("Seleccione a qué se aplica la promoción.");
            if (p.FechaFin.Date < p.FechaInicio.Date)
                throw new ReglaNegocioException("La fecha de fin no puede ser anterior a la de inicio.");
            if (p.DiasSemana != null && (p.DiasSemana.Length == 0 || p.DiasSemana.Any(c => c < '1' || c > '7') ||
                                         p.DiasSemana.Distinct().Count() != p.DiasSemana.Length))
                throw new ReglaNegocioException("Los días de la semana no son válidos.");
            if (p.MontoMinimo.HasValue && (p.MontoMinimo.Value < 0 || decimal.Round(p.MontoMinimo.Value, 2) != p.MontoMinimo.Value))
                throw new ReglaNegocioException("El monto mínimo debe ser 0 o mayor, con hasta 2 decimales.");
            if (p.PorPlatos && p.Platos.Count == 0)
                throw new ReglaNegocioException("Seleccione al menos un plato participante.");
            if (!p.PorPlatos)
                p.Platos.Clear();
        }

        // ------------------------------------------------------
        // ESCRITURAS (Administrador; transacción Serializable)
        // ------------------------------------------------------
        public static int Guardar(PromocionInfo p, int idOperador)
        {
            Validar(p);
            return EmpleadoDatos.Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idOperador);

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM dbo.PROMOCION WITH (UPDLOCK, HOLDLOCK)
                    WHERE nombre = @Nombre AND id_promocion <> @Id;", cx, tx))
                {
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = p.Nombre;
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = p.IdPromocion;
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                        throw new ReglaNegocioException("Ya existe una promoción llamada \"" + p.Nombre + "\".");
                }

                foreach (int idPlato in p.Platos)
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.PLATO WHERE id_plato = @Id;", cx, tx))
                    {
                        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idPlato;
                        if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                            throw new ReglaNegocioException("Uno de los platos seleccionados ya no existe.");
                    }
                }

                int id = p.IdPromocion;
                string sql = id == 0
                    ? @"INSERT INTO dbo.PROMOCION (nombre, tipo_descuento, valor, alcance, fecha_inicio, fecha_fin, estado, descripcion, dias_semana, monto_minimo)
                        VALUES (@Nombre, @Tipo, @Valor, @Alcance, @Inicio, @Fin, @Estado, @Descripcion, @Dias, @Minimo);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);"
                    : @"UPDATE dbo.PROMOCION
                           SET nombre = @Nombre, tipo_descuento = @Tipo, valor = @Valor, alcance = @Alcance,
                               fecha_inicio = @Inicio, fecha_fin = @Fin, descripcion = @Descripcion,
                               dias_semana = @Dias, monto_minimo = @Minimo
                         WHERE id_promocion = @Id;
                        SELECT CASE WHEN @@ROWCOUNT = 1 THEN @Id ELSE 0 END;";
                using (SqlCommand cmd = new SqlCommand(sql, cx, tx))
                {
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = p.Nombre;
                    cmd.Parameters.Add("@Tipo", SqlDbType.VarChar, 20).Value = p.TipoDescuento;
                    SqlParameter v = cmd.Parameters.Add("@Valor", SqlDbType.Decimal); v.Precision = 10; v.Scale = 2; v.Value = p.Valor;
                    cmd.Parameters.Add("@Alcance", SqlDbType.VarChar, 20).Value = p.Alcance;
                    cmd.Parameters.Add("@Inicio", SqlDbType.Date).Value = p.FechaInicio.Date;
                    cmd.Parameters.Add("@Fin", SqlDbType.Date).Value = p.FechaFin.Date;
                    cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = "activo";
                    cmd.Parameters.Add("@Descripcion", SqlDbType.VarChar, 255).Value = p.Descripcion.Length == 0 ? (object)DBNull.Value : p.Descripcion;
                    cmd.Parameters.Add("@Dias", SqlDbType.VarChar, 7).Value = (object)p.DiasSemana ?? DBNull.Value;
                    SqlParameter m = cmd.Parameters.Add("@Minimo", SqlDbType.Decimal); m.Precision = 10; m.Scale = 2;
                    m.Value = p.MontoMinimo.HasValue ? (object)p.MontoMinimo.Value : DBNull.Value;
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    id = Convert.ToInt32(cmd.ExecuteScalar());
                }
                if (id == 0)
                    throw new ReglaNegocioException("La promoción ya no existe.");

                // Platos participantes: se reemplaza la lista completa.
                using (SqlCommand cmd = new SqlCommand("DELETE FROM dbo.PROMOCION_PLATO WHERE id_promocion = @Id;", cx, tx))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    cmd.ExecuteNonQuery();
                }
                foreach (int idPlato in p.Platos.Distinct())
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "INSERT INTO dbo.PROMOCION_PLATO (id_promocion, id_plato) VALUES (@Id, @Plato);", cx, tx))
                    {
                        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                        cmd.Parameters.Add("@Plato", SqlDbType.Int).Value = idPlato;
                        cmd.ExecuteNonQuery();
                    }
                }

                Registro.Info("Promociones", "Empleado " + idOperador + (p.IdPromocion == 0 ? " registró" : " actualizó") +
                    " la promoción " + id + " (" + p.Nombre + ", " + p.DescuentoTexto + ").");
                return id;
            });
        }

        public static void CambiarEstado(int idPromocion, bool activa, int idOperador)
        {
            EmpleadoDatos.Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idOperador);
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE dbo.PROMOCION SET estado = @Estado WHERE id_promocion = @Id;", cx, tx))
                {
                    cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = activa ? "activo" : "inactivo";
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idPromocion;
                    if (cmd.ExecuteNonQuery() != 1)
                        throw new ReglaNegocioException("La promoción ya no existe.");
                }
                Registro.Info("Promociones", "Empleado " + idOperador + (activa ? " activó" : " desactivó") + " la promoción " + idPromocion + ".");
                return 0;
            });
        }

        // ------------------------------------------------------
        // MOTOR DE DESCUENTOS (sin acceso a SQL; listo para Pedidos)
        // ------------------------------------------------------
        public static ResultadoPromocion Calcular(PromocionInfo p, IList<LineaDescuento> lineas, DateTime fecha)
        {
            ResultadoPromocion r = new ResultadoPromocion();
            foreach (LineaDescuento l in lineas)
                l.DescuentoUnitario = 0;

            r.Subtotal = lineas.Sum(l => decimal.Round(l.Cantidad * l.PrecioUnitario, 2));

            string situacion = p.Situacion(fecha);
            if (situacion != "Vigente hoy")
            {
                r.Motivo = situacion == "Vigente (otro día)"
                    ? "La promoción solo aplica: " + p.DiasTexto + "."
                    : "La promoción no está vigente (" + situacion.ToLowerInvariant() + ").";
                return r;
            }
            if (p.MontoMinimo.HasValue && r.Subtotal < p.MontoMinimo.Value)
            {
                r.Motivo = "El pedido no alcanza el monto mínimo de S/ " +
                    p.MontoMinimo.Value.ToString("0.00", CultureInfo.InvariantCulture) + ".";
                return r;
            }

            if (p.PorPlatos)
            {
                HashSet<int> elegibles = new HashSet<int>(p.Platos);
                foreach (LineaDescuento l in lineas.Where(x => x.Tipo == "plato" && elegibles.Contains(x.IdProducto)))
                {
                    decimal d = p.TipoDescuento == "porcentaje"
                        ? decimal.Round(l.PrecioUnitario * p.Valor / 100m, 2, MidpointRounding.AwayFromZero)
                        : p.Valor;
                    l.DescuentoUnitario = Math.Min(Math.Max(d, 0), l.PrecioUnitario);
                }
                r.DescuentoLineas = lineas.Sum(l => l.Cantidad * l.DescuentoUnitario);
                if (r.DescuentoLineas <= 0)
                {
                    r.Motivo = "El pedido no tiene platos participantes de esta promoción.";
                    return r;
                }
            }
            else
            {
                decimal d = p.TipoDescuento == "porcentaje"
                    ? decimal.Round(r.Subtotal * p.Valor / 100m, 2, MidpointRounding.AwayFromZero)
                    : p.Valor;
                r.DescuentoPedido = Math.Min(Math.Max(d, 0), r.Subtotal);
                if (r.DescuentoPedido <= 0)
                {
                    r.Motivo = "El pedido no tiene importe para descontar.";
                    return r;
                }
            }

            r.Aplicable = true;
            return r;
        }
    }
}
