using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // MENÚ LATERAL COMÚN
    // ----------------------------------------------------------
    // Todas las ventanas principales comparten la barra negra con
    // los mismos nombres de botón. En el diseño, muchos botones no
    // tenían evento, otros tenían el método vacío o sin conectar.
    // Aquí, en un solo lugar:
    //  * cada botón (y la etiqueta y el ícono dibujados encima)
    //    abre su módulo con el Navegador (que valida permisos);
    //  * "Cerrar Sesión" cierra la sesión;
    //  * las opciones que el cargo no puede abrir se ven atenuadas
    //    y deshabilitadas (el Navegador igual las bloquea).
    // Se quitan los eventos Click heredados de esos botones para no
    // navegar dos veces (solo navegaban o estaban vacíos).
    // ==========================================================
    internal static class MenuLateral
    {
        private static readonly Dictionary<string, Type> Destinos = new Dictionary<string, Type>
        {
            { "ButtonINICIO", typeof(FRM_INICIO) },
            { "guna2Button1", typeof(FRM_MESAS) },
            { "guna2Button2", typeof(FRMS_PEDIDOS) },
            { "guna2Button5", typeof(FRM_COCINA) },
            { "guna2Button4", typeof(FRM_CLIENTES) },
            { "guna2Button3", typeof(FRM_EMPLEADOS) },
            { "guna2Button9", typeof(FRM_MENU) },
            { "guna2Button8", typeof(FRM_INVENTARIO) },
            { "guna2Button7", typeof(FRM_PROMOCIONES) },
            { "guna2Button6", typeof(FRM_REPORTES) }
        };

        private static readonly Color Atenuado = Color.FromArgb(110, 110, 110);

        public static void Conectar(Form f)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || f == null)
                return;

            // Solo ventanas con la barra lateral completa (no Inicio de sesión).
            Control inicio = Buscar(f, "ButtonINICIO"), cerrar = Buscar(f, "but_Cerrar");
            if (inicio == null || cerrar == null || inicio.Parent == null)
                return;
            Control barra = inicio.Parent;

            foreach (KeyValuePair<string, Type> d in Destinos)
            {
                Control boton = Buscar(f, d.Key);
                if (boton == null || boton.Parent != barra)
                    continue;
                Type destino = d.Value;
                QuitarClicks(boton);
                EventHandler ir = (s, e) =>
                {
                    if (!boton.Enabled) return;
                    if (destino == f.GetType()) return;          // ya está en este módulo
                    Navegador.Ir(destino, f);
                };
                boton.Click += ir;
                foreach (Control encima in Encima(barra, boton))
                {
                    QuitarClicks(encima);
                    encima.Click += ir;
                    encima.Cursor = Cursors.Hand;
                }
                boton.Cursor = Cursors.Hand;
            }

            QuitarClicks(cerrar);
            EventHandler salir = (s, e) => Navegador.CerrarSesion();
            cerrar.Click += salir;
            foreach (Control encima in Encima(barra, cerrar))
            {
                QuitarClicks(encima);
                encima.Click += salir;
                encima.Cursor = Cursors.Hand;
            }

            AplicarPermisos(f, barra);
            f.VisibleChanged += (s, e) => { if (f.Visible) AplicarPermisos(f, barra); };
        }

        // Opciones del menú según el cargo de la sesión.
        private static void AplicarPermisos(Form f, Control barra)
        {
            string cargo = SesionActual.Iniciada ? SesionActual.Cargo : null;
            foreach (KeyValuePair<string, Type> d in Destinos)
            {
                Control boton = Buscar(f, d.Key);
                if (boton == null || boton.Parent != barra)
                    continue;
                bool permitido = cargo == null || Permisos.PuedeAbrir(d.Value, cargo);
                boton.Enabled = permitido;
                foreach (Control encima in Encima(barra, boton))
                {
                    if (encima is PictureBox) continue;
                    if (encima.Tag == null) encima.Tag = encima.ForeColor;      // color original
                    encima.ForeColor = permitido ? (Color)encima.Tag : Atenuado;
                    encima.Cursor = permitido ? Cursors.Hand : Cursors.Default;
                }
            }
        }

        // Etiquetas e íconos dibujados sobre el botón (en la misma barra).
        private static List<Control> Encima(Control barra, Control boton)
        {
            List<Control> r = new List<Control>();
            foreach (Control c in barra.Controls)
            {
                if (c == boton || c is ButtonBase || c.GetType().Name.Contains("Button")) continue;
                Rectangle a = c.Bounds; a.Inflate(-2, -2);
                if (a.IntersectsWith(boton.Bounds)) r.Add(c);
            }
            return r;
        }

        private static readonly FieldInfo EventoClick =
            typeof(Control).GetField("EventClick", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly PropertyInfo Eventos =
            typeof(Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void QuitarClicks(Control c)
        {
            if (EventoClick == null || Eventos == null) return;
            object clave = EventoClick.GetValue(null);
            EventHandlerList lista = Eventos.GetValue(c, null) as EventHandlerList;
            if (lista == null || clave == null) return;
            Delegate actual = lista[clave];
            if (actual != null) lista.RemoveHandler(clave, actual);
        }

        private static Control Buscar(Control raiz, string nombre)
        {
            Control[] r = raiz.Controls.Find(nombre, true);
            return r.Length == 0 ? null : r[0];
        }
    }
}
