using System;
using System.Collections.Generic;

namespace PROYECTO_RESTAURANTE
{
    // Reglas únicas de reservas. Las usan Nueva reserva (botón superior y
    // botón «Reservar» del panel de la mesa), el monitor automático y la
    // confirmación de llegada, para que no existan dos criterios distintos.
    internal static class ReservaReglas
    {
        // Horario de atención del restaurante: 11:00 a 23:00.
        public static readonly TimeSpan HoraApertura = new TimeSpan(11, 0, 0);
        public static readonly TimeSpan HoraCierre = new TimeSpan(23, 0, 0);

        // Las reservas se ofrecen cada 30 minutos.
        public const int IntervaloMinutos = 30;

        // Atención mínima antes del cierre: la última reserva es a las 22:00,
        // para que el cliente tenga al menos una hora de servicio.
        public const int AtencionMinimaMinutos = 60;

        // Tiempo que una reserva ocupa la mesa. Dos reservas de la misma
        // mesa deben separarse al menos este tiempo (19:00 y 21:00 sí;
        // 19:00 y 20:30 no).
        public const int DuracionReservaMinutos = 120;

        // Tolerancia de llegada: pasado este tiempo la reserva vence.
        public const int ToleranciaLlegadaMinutos = 30;

        // La mesa pasa a «reservada» este tiempo antes de la hora reservada
        // (al registrar la reserva o mediante el monitor automático).
        public const int ApartadoMinutos = 60;

        public const int DiasAnticipacionMaxima = 30;
        public const int MaxPersonasPorMesa = 4;

        public static TimeSpan UltimoHorario
        {
            get { return HoraCierre - TimeSpan.FromMinutes(AtencionMinimaMinutos); }
        }

        // Horarios ofrecidos para una fecha. Si es hoy, solo los posteriores
        // a «ahora»; si es una fecha posterior, toda la jornada.
        public static List<DateTime> Horarios(DateTime fecha, DateTime ahora)
        {
            List<DateTime> lista = new List<DateTime>();
            DateTime dia = fecha.Date;
            if (dia < ahora.Date || dia > ahora.Date.AddDays(DiasAnticipacionMaxima))
                return lista;

            for (DateTime hora = dia.Add(HoraApertura);
                 hora <= dia.Add(UltimoHorario);
                 hora = hora.AddMinutes(IntervaloMinutos))
            {
                if (hora > ahora)
                    lista.Add(hora);
            }
            return lista;
        }

        public static bool HorarioValido(DateTime fechaHora, DateTime ahora)
        {
            if (fechaHora <= ahora) return false;
            if (fechaHora.Date > ahora.Date.AddDays(DiasAnticipacionMaxima)) return false;
            if (fechaHora.Second != 0 || fechaHora.Millisecond != 0) return false;
            if (fechaHora.Minute % IntervaloMinutos != 0) return false;
            TimeSpan t = fechaHora.TimeOfDay;
            return t >= HoraApertura && t <= UltimoHorario;
        }

        // Dos reservas de la misma mesa chocan si están a menos de la duración.
        public static bool EnConflicto(DateTime a, DateTime b)
        {
            return Math.Abs((a - b).TotalMinutes) < DuracionReservaMinutos;
        }

        // Una reserva cercana necesita la mesa libre AHORA (no ocupada por
        // un pedido en curso); una lejana solo necesita que no choque con
        // otra reserva.
        public static bool RequiereMesaLibreAhora(DateTime fechaHora, DateTime ahora)
        {
            return fechaHora < ahora.AddMinutes(DuracionReservaMinutos);
        }

        public static bool SeApartaAlRegistrar(DateTime fechaHora, DateTime ahora)
        {
            return fechaHora <= ahora.AddMinutes(ApartadoMinutos);
        }

        public static string TextoHorario()
        {
            return DateTime.Today.Add(HoraApertura).ToString("h:mm tt", Cultura) + " a " +
                   DateTime.Today.Add(HoraCierre).ToString("h:mm tt", Cultura) +
                   " (última reserva " + DateTime.Today.Add(UltimoHorario).ToString("HH:mm") + ")";
        }

        private static readonly System.Globalization.CultureInfo Cultura =
            System.Globalization.CultureInfo.GetCultureInfo("es-PE");
    }
}
