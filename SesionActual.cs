using System;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // Sesión del empleado autenticado en INICIAR_SESIÓN.
    // Solo guarda datos de identificación (nunca contraseñas).
    // Las operaciones sensibles vuelven a comprobar en SQL Server
    // que el empleado sigue activo, con acceso y con el cargo
    // requerido: esta clase NO es una autorización por sí sola.
    // ==========================================================
    public static class SesionActual
    {
        public static int? IdEmpleado { get; private set; }
        public static string Usuario { get; private set; }
        public static string NombreCompleto { get; private set; }
        public static string Cargo { get; private set; }
        public static int IdSucursal { get; private set; }
        public static DateTime? Inicio { get; private set; }

        public static bool Iniciada
        {
            get { return IdEmpleado.HasValue; }
        }

        // Nombre corto para la cabecera: "Ana Ruiz".
        public static string NombreCorto
        {
            get
            {
                string nombre = (NombreCompleto ?? "").Trim();
                string[] partes = nombre.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return partes.Length >= 2 ? partes[0] + " " + partes[partes.Length - 1] : nombre;
            }
        }

        // Compatibilidad con el código anterior.
        public static void IniciarSesion(int idEmpleado)
        {
            IniciarSesion(idEmpleado, null, null, null, BaseDatos.IdSucursalActual);
        }

        public static void IniciarSesion(
            int idEmpleado, string usuario, string nombreCompleto,
            string cargo, int idSucursal)
        {
            if (idEmpleado <= 0)
            {
                throw new Exception(
                    "El identificador del empleado no es válido.");
            }

            IdEmpleado = idEmpleado;
            Usuario = usuario;
            NombreCompleto = nombreCompleto;
            Cargo = cargo;
            IdSucursal = idSucursal;
            Inicio = DateTime.Now;
        }

        public static void CerrarSesion()
        {
            IdEmpleado = null;
            Usuario = null;
            NombreCompleto = null;
            Cargo = null;
            IdSucursal = 0;
            Inicio = null;
        }
    }
}
