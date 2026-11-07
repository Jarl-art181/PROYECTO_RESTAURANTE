using System;
using System.Data;
using System.Data.SqlClient;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // GENERAR RECIBO · REGISTRO DEL COMPRADOR EN GESTIÓN DE CLIENTES
    // ----------------------------------------------------------
    // Solo lógica (el diseño de la ventana no cambia). Antes de cobrar,
    // si el comprador figura en el recibo con documento y no es un
    // cliente registrado, se pide su autorización en un diálogo aparte.
    // El registro (o el vínculo, si el documento ya existe) se hace en
    // la MISMA transacción del cobro: si el cobro falla, no queda nada.
    // ==========================================================
    public partial class Generar_Boleta
    {
        private ClienteInfo compradorPorRegistrar;      // autorizado: se registra al cobrar
        private int? clienteRegistradoAlCobrar;          // id resultante (nuevo o existente)

        // false = el usuario canceló (no se cobra).
        private bool AutorizarRegistroComprador()
        {
            compradorPorRegistrar = null;
            clienteRegistradoAlCobrar = null;

            if (!IncluirCliente())
                return true;
            string documento = txtDNICliente.Text.Trim().ToUpperInvariant();
            if (documento.Length == 0)
                return true;                                  // sin documento no se registra
            if (idClienteSeleccionado.HasValue && documento == documentoClienteRegistrado)
                return true;                                  // ya es un cliente registrado

            // ¿El documento ya existe? Activo: se vincula sin preguntar (no se
            // duplica). Inactivo: no se vincula ni se reactiva desde aquí.
            string estado = EstadoClientePorDocumento(documento);
            if (estado == "inactivo")
                return true;
            if (estado == "activo")
            {
                compradorPorRegistrar = new ClienteInfo { Documento = documento };
                return true;
            }

            ClienteInfo c = new ClienteInfo { Documento = documento, Telefono = ClienteDatos.SoloDigitos(txtCelularCliente.Text) };
            string nombre, apellidos;
            ClienteDatos.SepararNombre(txtNombreCliente.Text, out nombre, out apellidos);
            c.Nombre = nombre;
            c.Apellidos = apellidos;
            string motivo = null;
            try { ClienteDatos.Validar(c); }
            catch (ReglaNegocioException ex) { motivo = ex.Message; }

            bool promociones;
            DialogoAutorizacionCliente.Resultado r = DialogoAutorizacionCliente.Mostrar(this, c, motivo, out promociones);
            if (r == DialogoAutorizacionCliente.Resultado.Cancelar)
                return false;
            if (r == DialogoAutorizacionCliente.Resultado.Registrar)
            {
                c.AceptaPromociones = promociones;
                compradorPorRegistrar = c;
            }
            return true;
        }

        private string EstadoClientePorDocumento(string documento)
        {
            try
            {
                using (SqlConnection cx = new SqlConnection(cadenaConexion))
                using (SqlCommand cmd = new SqlCommand("SELECT TOP (1) estado FROM dbo.CLIENTE WHERE documento = @Doc;", cx))
                {
                    cmd.CommandTimeout = 10;
                    cmd.Parameters.Add("@Doc", SqlDbType.VarChar, 20).Value = documento;
                    cx.Open();
                    return cmd.ExecuteScalar() as string;
                }
            }
            catch (Exception ex)
            {
                Registro.Error("Boleta", ex);
                return null;                                  // se resuelve igual dentro del cobro
            }
        }

        // Dentro de la transacción del cobro.
        private int? RegistrarCompradorEnCobro(SqlConnection cx, SqlTransaction tx, int idCajero)
        {
            if (compradorPorRegistrar == null)
                return null;
            return ClienteDatos.RegistrarDesdeCobro(cx, tx, compradorPorRegistrar, idCajero);
        }
    }
}
