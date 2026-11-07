using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // EXPORTACIÓN DE REPORTES A EXCEL (.xlsx) Y PDF
    // ----------------------------------------------------------
    // Sin librerías externas:
    //  * Excel: libro Office Open XML (zip + XML) con una hoja por
    //    tabla, encabezado en negrita y números como números.
    //  * PDF: documento A4 horizontal con título, filtros, tablas
    //    (columnas proporcionales) y numeración de páginas. Letra
    //    Helvetica con codificación WinAnsi (tildes y ñ).
    // ==========================================================
    internal sealed class SeccionReporte
    {
        public string Titulo;
        public DataTable Tabla;
    }

    internal static class ExportadorReportes
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------
        // EXCEL
        // ------------------------------------------------------
        public static void Excel(string ruta, string titulo, string filtros, IList<SeccionReporte> secciones)
        {
            if (File.Exists(ruta)) File.Delete(ruta);
            using (FileStream fs = new FileStream(ruta, FileMode.CreateNew))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                List<string> nombres = new List<string>();
                foreach (SeccionReporte s in secciones)
                    nombres.Add(NombreHoja(s.Titulo, nombres));

                Escribir(zip, "[Content_Types].xml", ContentTypes(secciones.Count));
                Escribir(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                StringBuilder libro = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
                StringBuilder rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                for (int i = 0; i < secciones.Count; i++)
                {
                    libro.Append("<sheet name=\"" + Xml(nombres[i]) + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>");
                    rels.Append("<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>");
                }
                libro.Append("</sheets></workbook>");
                rels.Append("<Relationship Id=\"rId" + (secciones.Count + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                Escribir(zip, "xl/workbook.xml", libro.ToString());
                Escribir(zip, "xl/_rels/workbook.xml.rels", rels.ToString());
                Escribir(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/></numFmts>" +
                    "<fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"14\"/><name val=\"Calibri\"/></font></fonts>" +
                    "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                    "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFD700\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                    "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"5\">" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +                                 // 0 texto
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" + // 1 encabezado
                    "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +       // 2 decimal
                    "<xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +         // 3 entero
                    "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                 // 4 título
                    "</cellXfs></styleSheet>");

                for (int i = 0; i < secciones.Count; i++)
                    Escribir(zip, "xl/worksheets/sheet" + (i + 1) + ".xml", Hoja(titulo, filtros, secciones[i]));
            }
        }

        private static string Hoja(string titulo, string filtros, SeccionReporte s)
        {
            DataTable t = s.Tabla;
            StringBuilder sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols>");
            for (int c = 0; c < t.Columns.Count; c++)
            {
                int ancho = Math.Max(t.Columns[c].ColumnName.Length, 10);
                foreach (DataRow r in t.Rows) ancho = Math.Max(ancho, Convert.ToString(r[c]).Length);
                sb.Append("<col min=\"" + (c + 1) + "\" max=\"" + (c + 1) + "\" width=\"" + Math.Min(60, ancho + 3) + "\" customWidth=\"1\"/>");
            }
            sb.Append("</cols><sheetData>");
            sb.Append(Fila(1, new[] { Celda("A1", titulo + " - " + s.Titulo, 4) }));
            sb.Append(Fila(2, new[] { Celda("A2", filtros, 0) }));
            List<string> enc = new List<string>();
            for (int c = 0; c < t.Columns.Count; c++) enc.Add(Celda(Col(c) + "4", t.Columns[c].ColumnName, 1));
            sb.Append(Fila(4, enc));
            int fila = 5;
            foreach (DataRow r in t.Rows)
            {
                List<string> celdas = new List<string>();
                for (int c = 0; c < t.Columns.Count; c++)
                {
                    string refe = Col(c) + fila;
                    object v = r[c];
                    if (v == DBNull.Value) continue;
                    if (v is decimal || v is double || v is float)
                        celdas.Add("<c r=\"" + refe + "\" s=\"2\"><v>" + Convert.ToDecimal(v).ToString(Inv) + "</v></c>");
                    else if (v is int || v is long || v is short)
                        celdas.Add("<c r=\"" + refe + "\" s=\"3\"><v>" + Convert.ToInt64(v).ToString(Inv) + "</v></c>");
                    else
                        celdas.Add(Celda(refe, Convert.ToString(v), 0));
                }
                sb.Append(Fila(fila++, celdas));
            }
            if (t.Rows.Count == 0)
                sb.Append(Fila(5, new[] { Celda("A5", "Sin datos en el período seleccionado.", 0) }));
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string Fila(int n, IEnumerable<string> celdas)
        {
            return "<row r=\"" + n + "\">" + string.Concat(celdas) + "</row>";
        }

        private static string Celda(string refe, string texto, int estilo)
        {
            return "<c r=\"" + refe + "\" t=\"inlineStr\" s=\"" + estilo + "\"><is><t xml:space=\"preserve\">" + Xml(texto) + "</t></is></c>";
        }

        private static string Col(int i)
        {
            string s = "";
            for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
            return s;
        }

        private static string NombreHoja(string titulo, List<string> usados)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in titulo ?? "Hoja")
                if ("[]:*?/\\".IndexOf(ch) < 0) sb.Append(ch);
            string n = sb.ToString().Trim();
            if (n.Length == 0) n = "Hoja";
            if (n.Length > 28) n = n.Substring(0, 28);
            string baseN = n; int k = 2;
            while (usados.Contains(n)) n = baseN + " " + (k++);
            return n;
        }

        private static string ContentTypes(int hojas)
        {
            StringBuilder sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 1; i <= hojas; i++)
                sb.Append("<Override PartName=\"/xl/worksheets/sheet" + i + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sb.Append("</Types>");
            return sb.ToString();
        }

        private static void Escribir(ZipArchive zip, string nombre, string contenido)
        {
            ZipArchiveEntry e = zip.CreateEntry(nombre, CompressionLevel.Optimal);
            using (StreamWriter w = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                w.Write(contenido);
        }

        private static string Xml(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s)
                if (ch == 0x9 || ch == 0xA || ch == 0xD || ch >= 0x20) sb.Append(ch);
            return SecurityElement.Escape(sb.ToString());
        }

        // ------------------------------------------------------
        // PDF
        // ------------------------------------------------------
        private const float AnchoPagina = 842f, AltoPagina = 595f, Margen = 36f;   // A4 horizontal

        public static void Pdf(string ruta, string titulo, string filtros, IList<SeccionReporte> secciones)
        {
            List<StringBuilder> paginas = new List<StringBuilder>();
            StringBuilder p = null;
            float y = 0;
            Action nuevaPagina = () =>
            {
                p = new StringBuilder();
                paginas.Add(p);
                // Franja amarilla del encabezado.
                p.Append("1 0.843 0 rg 0 " + N(AltoPagina - 46) + " " + N(AnchoPagina) + " 46 re f 0 0 0 rg\n");
                Texto(p, Margen, AltoPagina - 30, 16, true, "AL ESTILO PIURANO - " + titulo);
                Texto(p, Margen, AltoPagina - 62, 9, false, filtros + "   ·   Generado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                y = AltoPagina - 84;
            };
            nuevaPagina();

            foreach (SeccionReporte s in secciones)
            {
                DataTable t = s.Tabla;
                if (y < 120) nuevaPagina();
                Texto(p, Margen, y, 12, true, s.Titulo);
                y -= 18;

                int nCol = Math.Max(1, t.Columns.Count);
                float[] pesos = new float[nCol];
                for (int c = 0; c < t.Columns.Count; c++)
                {
                    float w = Math.Max(6, t.Columns[c].ColumnName.Length);
                    foreach (DataRow r in t.Rows) w = Math.Max(w, Math.Min(45, Convert.ToString(r[c]).Length));
                    pesos[c] = w;
                }
                float total = 0; foreach (float w in pesos) total += w;
                float util = AnchoPagina - 2 * Margen;
                float[] anchos = new float[nCol];
                for (int c = 0; c < nCol; c++) anchos[c] = util * pesos[c] / total;

                Action encabezado = () =>
                {
                    p.Append("1 0.843 0 rg " + N(Margen) + " " + N(y - 4) + " " + N(util) + " 16 re f 0 0 0 rg\n");
                    float x = Margen + 3;
                    for (int c = 0; c < t.Columns.Count; c++) { Texto(p, x, y, 8, true, Recortar(t.Columns[c].ColumnName, anchos[c], 8)); x += anchos[c]; }
                    y -= 16;
                };
                encabezado();
                if (t.Rows.Count == 0) { Texto(p, Margen + 3, y, 9, false, "Sin datos en el período seleccionado."); y -= 14; }
                bool alterna = false;
                foreach (DataRow r in t.Rows)
                {
                    if (y < Margen + 20) { nuevaPagina(); Texto(p, Margen, y, 10, true, s.Titulo + " (continúa)"); y -= 16; encabezado(); }
                    if (alterna) p.Append("0.96 0.96 0.96 rg " + N(Margen) + " " + N(y - 4) + " " + N(util) + " 14 re f 0 0 0 rg\n");
                    alterna = !alterna;
                    float x = Margen + 3;
                    for (int c = 0; c < t.Columns.Count; c++)
                    {
                        object v = r[c];
                        string txt = v == DBNull.Value ? "" : v is decimal ? ((decimal)v).ToString("N2", CultureInfo.GetCultureInfo("es-PE")) : Convert.ToString(v);
                        Texto(p, x, y, 8, false, Recortar(txt, anchos[c], 8));
                        x += anchos[c];
                    }
                    y -= 14;
                }
                y -= 12;
            }

            for (int i = 0; i < paginas.Count; i++)
                Texto(paginas[i], AnchoPagina - Margen - 70, 18, 8, false, "Página " + (i + 1) + " de " + paginas.Count);

            EscribirPdf(ruta, paginas);
        }

        private static string Recortar(string t, float ancho, float tam)
        {
            int max = (int)(ancho / (tam * 0.52f)) - 1;
            if (max < 3) max = 3;
            return t.Length <= max ? t : t.Substring(0, max - 1) + "…";
        }

        private static void Texto(StringBuilder p, float x, float y, float tam, bool negrita, string texto)
        {
            p.Append("BT /" + (negrita ? "F2" : "F1") + " " + N(tam) + " Tf " + N(x) + " " + N(y) + " Td (" + EscaparPdf(texto) + ") Tj ET\n");
        }

        private static string N(float v) { return v.ToString("0.##", Inv); }

        private static readonly Encoding WinAnsi = Encoding.GetEncoding(1252);

        private static string EscaparPdf(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in WinAnsi.GetBytes(s ?? ""))
            {
                if (b == '(' || b == ')' || b == '\\') sb.Append('\\').Append((char)b);
                else if (b < 32 || b > 126) sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                else sb.Append((char)b);
            }
            return sb.ToString();
        }

        private static void EscribirPdf(string ruta, List<StringBuilder> paginas)
        {
            List<string> objetos = new List<string>();
            // 1 catálogo, 2 páginas, 3-4 fuentes, luego (contenido, página) por cada página.
            objetos.Add("<< /Type /Catalog /Pages 2 0 R >>");
            StringBuilder kids = new StringBuilder();
            for (int i = 0; i < paginas.Count; i++) kids.Append((7 + i * 2) + " 0 R ");   // 6 = contenido, 7 = página
            objetos.Add("<< /Type /Pages /Kids [" + kids + "] /Count " + paginas.Count + " >>");
            objetos.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            objetos.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            objetos.Add("<< /Producer (AL ESTILO PIURANO) >>");
            foreach (StringBuilder p in paginas)
            {
                string contenido = p.ToString();
                objetos.Add("<< /Length " + WinAnsi.GetByteCount(contenido) + " >>\nstream\n" + contenido + "endstream");
                objetos.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + N(AnchoPagina) + " " + N(AltoPagina) + "] " +
                            "/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents " + objetos.Count + " 0 R >>");
            }

            using (FileStream fs = new FileStream(ruta, FileMode.Create))
            {
                List<long> posiciones = new List<long>();
                Action<string> w = s => { byte[] b = WinAnsi.GetBytes(s); fs.Write(b, 0, b.Length); };
                w("%PDF-1.4\n%âãÏÓ\n");
                for (int i = 0; i < objetos.Count; i++)
                {
                    posiciones.Add(fs.Position);
                    w((i + 1) + " 0 obj\n" + objetos[i] + "\nendobj\n");
                }
                long xref = fs.Position;
                StringBuilder x = new StringBuilder("xref\n0 " + (objetos.Count + 1) + "\n0000000000 65535 f \n");
                foreach (long pos in posiciones) x.Append(pos.ToString("D10") + " 00000 n \n");
                x.Append("trailer\n<< /Size " + (objetos.Count + 1) + " /Root 1 0 R /Info 5 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
                w(x.ToString());
            }
        }
    }
}
