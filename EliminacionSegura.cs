using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // ELIMINACIÓN DEFINITIVA SEGURA
    // ----------------------------------------------------------
    // Desactivar  = el registro se conserva (con su historial) y
    //               puede reactivarse. Siempre disponible.
    // Eliminar    = DELETE real, solo si el registro no tiene NINGUNA
    //               relación histórica (auditoría de claves foráneas):
    //
    //   EMPLEADO  <- PEDIDO (mesero, cocinero, repartidor), PAGO (cajero),
    //                CIERRE_CAJA, MERMA, COMPRA (administrador)
    //   PLATO     <- DETALLE_PEDIDO_PLATO, DETALLE_COMBO, RECETA_PLATO,
    //                PROMOCION_PLATO, DEMO_RANKING_PLATO
    //   INSUMO    <- DETALLE_COMPRA, MERMA, RECETA_PLATO
    //
    // Reglas: sin DELETE en cascada (todas las FK son NO ACTION), en
    // una transacción con bloqueo de la fila, el historial se vuelve a
    // contar dentro de la transacción, solo un Administrador activo,
    // nunca el último Administrador habilitado ni la propia cuenta, y
    // cada eliminación queda en el registro de la aplicación.
    //
    // El borrado definitivo se habilita por base de datos con la clave
    // "PermitirEliminacionDefinitiva" de App.config (apagada por omisión).
    // ==========================================================
    internal static class EliminacionSegura
    {
        public static bool Habilitada
        {
            get
            {
                string valor = ConfigurationManager.AppSettings["PermitirEliminacionDefinitiva"];
                return string.Equals((valor ?? "").Trim(), "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public const string MotivoDeshabilitada =
            "La eliminación definitiva todavía no está habilitada en esta base de datos " +
            "(pendiente de autorización del Administrador del sistema).";

        // (consulta de conteo, descripción en singular, descripción en plural)
        private static readonly string[][] RelacionesEmpleado =
        {
            new[] { "SELECT COUNT(*) FROM dbo.PEDIDO WHERE id_mesero = @Id", "pedido atendido como mesero", "pedidos atendidos como mesero" },
            new[] { "SELECT COUNT(*) FROM dbo.PEDIDO WHERE id_cocinero = @Id", "pedido preparado en cocina", "pedidos preparados en cocina" },
            new[] { "SELECT COUNT(*) FROM dbo.PEDIDO WHERE id_empleado_reparto = @Id", "pedido repartido", "pedidos repartidos" },
            new[] { "SELECT COUNT(*) FROM dbo.PAGO WHERE id_cajero = @Id", "cobro registrado", "cobros registrados" },
            new[] { "SELECT COUNT(*) FROM dbo.CIERRE_CAJA WHERE id_cajero = @Id", "cierre de caja", "cierres de caja" },
            new[] { "SELECT COUNT(*) FROM dbo.MERMA WHERE id_empleado = @Id", "merma registrada", "mermas registradas" },
            new[] { "SELECT COUNT(*) FROM dbo.COMPRA WHERE id_administrador = @Id", "compra registrada", "compras registradas" }
        };

        private static readonly string[][] RelacionesPlato =
        {
            new[] { "SELECT COUNT(*) FROM dbo.DETALLE_PEDIDO_PLATO WHERE id_plato = @Id", "línea de pedido (ventas)", "líneas de pedidos (ventas)" },
            new[] { "SELECT COUNT(*) FROM dbo.DETALLE_COMBO WHERE id_plato = @Id", "combo que lo incluye", "combos que lo incluyen" },
            new[] { "SELECT COUNT(*) FROM dbo.RECETA_PLATO WHERE id_plato = @Id", "insumo en su receta", "insumos en su receta" },
            new[] { "SELECT COUNT(*) FROM dbo.PROMOCION_PLATO WHERE id_plato = @Id", "promoción asociada", "promociones asociadas" },
            new[] { "SELECT COUNT(*) FROM dbo.DEMO_RANKING_PLATO WHERE id_plato = @Id", "venta en el ranking de ventas", "ventas en el ranking de ventas" }
        };

        private static readonly string[][] RelacionesInsumo =
        {
            new[] { "SELECT COUNT(*) FROM dbo.DETALLE_COMPRA WHERE id_insumo = @Id", "compra (entrada de stock)", "compras (entradas de stock)" },
            new[] { "SELECT COUNT(*) FROM dbo.MERMA WHERE id_insumo = @Id", "merma (salida de stock)", "mermas (salidas de stock)" },
            new[] { "SELECT COUNT(*) FROM dbo.RECETA_PLATO WHERE id_insumo = @Id", "receta de plato que lo usa", "recetas de platos que lo usan" }
        };

        // ------------------------------------------------------
        // CONSULTA DEL HISTORIAL (para explicar antes de actuar)
        // ------------------------------------------------------
        public static List<string> HistorialEmpleado(int idEmpleado)
        {
            return EmpleadoDatos.Ejecutar((cx, tx) => Contar(cx, tx, RelacionesEmpleado, idEmpleado));
        }

        public static List<string> HistorialPlato(int idPlato)
        {
            return EmpleadoDatos.Ejecutar((cx, tx) => Contar(cx, tx, RelacionesPlato, idPlato));
        }

        public static List<string> HistorialInsumo(int idInsumo)
        {
            return EmpleadoDatos.Ejecutar((cx, tx) => Contar(cx, tx, RelacionesInsumo, idInsumo));
        }

        private static List<string> Contar(SqlConnection cx, SqlTransaction tx, string[][] relaciones, int id)
        {
            List<string> encontrados = new List<string>();
            foreach (string[] r in relaciones)
            {
                using (SqlCommand cmd = new SqlCommand(r[0] + ";", cx, tx))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    int n = Convert.ToInt32(cmd.ExecuteScalar());
                    if (n > 0)
                        encontrados.Add(n + " " + (n == 1 ? r[1] : r[2]));
                }
            }
            return encontrados;
        }

        private static void ExigirSinHistorial(SqlConnection cx, SqlTransaction tx, string[][] relaciones, int id, string quien)
        {
            List<string> historial = Contar(cx, tx, relaciones, id);
            if (historial.Count > 0)
            {
                throw new ReglaNegocioException(
                    "No se puede eliminar definitivamente " + quien + " porque tiene historial:\n• " +
                    string.Join("\n• ", historial) +
                    "\n\nEse historial debe conservarse. Puede desactivarlo en su lugar.");
            }
        }

        private static void ExigirHabilitada()
        {
            if (!Habilitada)
                throw new ReglaNegocioException(MotivoDeshabilitada);
        }

        private static int Borrar(SqlConnection cx, SqlTransaction tx, string sql, int id)
        {
            using (SqlCommand cmd = new SqlCommand(sql, cx, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                return cmd.ExecuteNonQuery();
            }
        }

        // ------------------------------------------------------
        // EMPLEADO
        // ------------------------------------------------------
        public static void EliminarEmpleado(int idEmpleado, int idAdministrador)
        {
            ExigirHabilitada();
            EmpleadoDatos.Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idAdministrador);
                if (idEmpleado == idAdministrador)
                    throw new ReglaNegocioException("No puede eliminar su propia cuenta.");

                EmpleadoInfo e = EmpleadoDatos.ObtenerBloqueado(cx, tx, idEmpleado);
                if (e.Cargo == Cargos.Administrador)
                    EmpleadoDatos.VerificarOtroAdministrador(cx, tx, idEmpleado);

                ExigirSinHistorial(cx, tx, RelacionesEmpleado, idEmpleado, "a " + e.NombreCompleto);

                // Primero su cargo (subtipo) y después el empleado; nada en cascada.
                foreach (string tabla in new[] { "ADMINISTRADOR", "CAJERO", "COCINERO", "MESERO", "REPARTIDOR" })
                    Borrar(cx, tx, "DELETE FROM dbo." + tabla + " WHERE id_empleado = @Id;", idEmpleado);
                if (Borrar(cx, tx, "DELETE FROM dbo.EMPLEADO WHERE id_empleado = @Id;", idEmpleado) != 1)
                    throw new ReglaNegocioException("El empleado ya no existe.");

                Registro.Info("Eliminacion", "Administrador " + idAdministrador + " eliminó definitivamente al empleado " +
                    idEmpleado + " (" + e.Cargo + ", sin historial).");
                return 0;
            });
        }

        // ------------------------------------------------------
        // PRODUCTO DEL MENÚ
        // ------------------------------------------------------
        public static void EliminarPlato(int idPlato, int idAdministrador)
        {
            ExigirHabilitada();
            EmpleadoDatos.Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idAdministrador);
                string nombre;
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT nombre FROM dbo.PLATO WITH (UPDLOCK, HOLDLOCK) WHERE id_plato = @Id;", cx, tx))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idPlato;
                    nombre = cmd.ExecuteScalar() as string;
                }
                if (nombre == null)
                    throw new ReglaNegocioException("El producto ya no existe.");

                ExigirSinHistorial(cx, tx, RelacionesPlato, idPlato, "\"" + nombre + "\"");
                Borrar(cx, tx, "DELETE FROM dbo.PLATO WHERE id_plato = @Id;", idPlato);

                Registro.Info("Eliminacion", "Administrador " + idAdministrador + " eliminó definitivamente el producto " +
                    idPlato + " (sin ventas, combos, recetas ni promociones).");
                return 0;
            });
        }

        // ------------------------------------------------------
        // INSUMO
        // ------------------------------------------------------
        public static void EliminarInsumo(int idInsumo, int idAdministrador)
        {
            ExigirHabilitada();
            EmpleadoDatos.Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idAdministrador);
                string nombre;
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT nombre FROM dbo.INSUMO WITH (UPDLOCK, HOLDLOCK)
                    WHERE id_insumo = @Id AND id_sucursal = @Sucursal;", cx, tx))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idInsumo;
                    cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                    nombre = cmd.ExecuteScalar() as string;
                }
                if (nombre == null)
                    throw new ReglaNegocioException("El insumo ya no existe.");

                ExigirSinHistorial(cx, tx, RelacionesInsumo, idInsumo, "el insumo \"" + nombre + "\"");
                Borrar(cx, tx, "DELETE FROM dbo.INSUMO WHERE id_insumo = @Id;", idInsumo);

                Registro.Info("Eliminacion", "Administrador " + idAdministrador + " eliminó definitivamente el insumo " +
                    idInsumo + " (sin compras, mermas ni recetas).");
                return 0;
            });
        }
    }
}
