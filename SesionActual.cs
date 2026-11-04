
using System;

namespace PROYECTO_RESTAURANTE
{
    public static class SesionActual
    {
        // Identificador del empleado autenticado.
        public static int? IdEmpleado { get; private set; }

        // Guardar la sesión después de validar
        // correctamente al empleado.
        public static void IniciarSesion(int idEmpleado)
        {
            if (idEmpleado <= 0)
            {
                throw new Exception(
                    "El identificador del empleado no es válido.");
            }

            IdEmpleado = idEmpleado;
        }

        // Finalizar la sesión.
        public static void CerrarSesion()
        {
            IdEmpleado = null;
        }
    }
}
