# Guía de imágenes · AL ESTILO PIURANO

Con esta guía puede **agregar o reemplazar imágenes sin tocar el código ni recompilar**: basta con colocar un archivo con el nombre correcto en la carpeta indicada.

## 1. Dónde están las carpetas

Junto al programa (`PROYECTO_RESTAURANTE.exe`) hay una carpeta `Imagenes` con cuatro subcarpetas:

```text
Imagenes\
├── Iconos\      menú lateral, barra superior y filtros (Todos, Platos, Bebidas, Salón…)
├── Botones\     nuevo, guardar, editar, eliminar, buscar, imprimir, cobrar, reservar, cancelar…
├── Paneles\     encabezados, tarjetas de indicadores, banner de Inicio y fotos de mesas
└── Productos\   fotos de los productos: plato_<id_plato>.jpg
```

- Al ejecutar desde Visual Studio: `bin\Debug\Imagenes\` (se crea al compilar y también al abrir el programa).
- En la carpeta del proyecto también existe `Imagenes\` con estas mismas subcarpetas: lo que coloque allí se copia a `bin\Debug` al compilar.

Las carpetas empiezan **vacías** (solo un `LEEME.txt`): mientras no coloque archivos, el programa muestra sus imágenes originales.

## 2. Cómo reemplazar una imagen

1. Busque en el **inventario** (sección 6) la ventana y el control; anote el **nombre del archivo** y la **ruta**.
2. Prepare su imagen en **PNG** (recomendado; admite transparencia) o **JPG**. También se aceptan `.jpeg` y `.bmp`.
3. Guárdela con ese nombre exacto en la carpeta indicada, por ejemplo `Imagenes\Botones\guardar.png`.
4. Vuelva a abrir la ventana (o cambie de módulo y regrese): la imagen nueva aparece **sin recompilar**.

Reglas útiles:

- **Un archivo sirve para todas las ventanas que comparten la función.** `Botones\guardar.png` cambia el ícono de Guardar en Clientes, Menú, Inventario, Pedidos y Reservar mesa; `Iconos\clientes.png` cambia el ícono «Clientes» del menú lateral en todas las ventanas.
- **No se deforma:** si su imagen tiene otra proporción, el programa la encuadra dentro del cuadro, sobre el mismo color de fondo del ícono original.
- **Tamaño:** use la *dimensión recomendada* (el doble del cuadro en pantalla, para que se vea nítida a 125 %). Si es más grande, el programa la reduce al cargarla; no hace falta recortarla.
- **Íconos de botones y menú:** PNG con fondo transparente o del mismo color que el botón (amarillo `#FFD700`, negro o blanco). Para mantener el estilo, use dibujos negros sencillos sobre amarillo (botones y filtros) o blancos sobre negro (menú lateral).
- **Fotos de productos:** JPG de 600 × 400 px (proporción 3:2), con el plato centrado y fondo claro.

## 3. Fotos de productos (Menú, Pedidos e Inicio)

Cada producto se identifica por su **id_plato** (no por su nombre ni por su posición). La foto se busca en este orden:

1. `Imagenes\Productos\plato_<id_plato>.jpg` (o `.png`), si existe;
2. la foto subida desde **Gestión de Menú → Cambiar imagen**;
3. la foto incluida en el programa;
4. `Imagenes\Productos\predeterminado.png`, o el recuadro «Sin foto».

La misma foto aparece en **Gestión de Menú**, **Gestión de Pedidos** e **Inicio → Productos más vendidos**. En Inicio, la foto acompaña al producto cuando cambia el ranking (que solo cuenta ventas cobradas).

## 4. Cómo restaurar la imagen original

- Borre el archivo personalizado, o cámbiele el nombre (por ejemplo `guardar_mio.png`), y vuelva a abrir la ventana.
- Las imágenes originales **nunca se modifican**: están dentro del programa y siempre quedan como respaldo.
- Si un archivo está dañado o no es una imagen, el programa lo ignora, muestra la original y lo anota en el registro (`%LOCALAPPDATA%\AlEstiloPiurano\registro.log`).

## 5. Cómo comprobar que se actualizó

1. Coloque el archivo y vuelva a abrir la ventana (o cambie de módulo y regrese).
2. Debe verse la imagen nueva en el control indicado en el inventario.
3. Si no cambia, revise: nombre exacto (sin espacios ni mayúsculas distintas en la extensión), carpeta correcta y que el archivo se abra en el visor de fotos de Windows.
4. En `registro.log` aparece una línea `Imagenes: <ventana>.<control> usa <ruta>` por cada imagen personalizada aplicada.

> El **logotipo oficial** del restaurante (menú lateral, inicio de sesión y recibo) no está en el inventario y no se reemplaza desde estas carpetas. El banner de Inicio sí es reemplazable, pero mantiene la imagen original mientras no coloque `banner_inicio.jpg`.

## 6. Íconos de los botones

Todos los botones con texto llevan el ícono de su función, tomado de un juego único de íconos PNG transparentes (`Resources\Iconos\`, 96 × 96 px, incluidos en el programa). El programa los pinta del color del texto del botón: negro sobre amarillo, blanco sobre verde, rojo o azul. Se reconocen por el **texto** del botón, así una misma acción usa el mismo ícono en todas las ventanas.

- Para reemplazar uno, coloque `Imagenes\Botones\<archivo>.png` (tabla 7.2). Su imagen se muestra **con sus propios colores**: use un dibujo del color del texto del botón (negro o blanco) sobre fondo transparente.
- Colores por acción, como en Gestión de Inventario: **Guardar** y **Reactivar** verde, **Eliminar**, **Desactivar** y **Registrar salida** rojo, **Registrar entrada** azul, **Cancelar** gris; el resto conserva su color.
- Si un botón cambia de texto en uso (Editar ↔ Cancelar, Eliminar ↔ Reactivar), su ícono y su color cambian con él.
- Los títulos de sección amarillos («Insumos de cocina», «Detalle del…», «Movimientos recientes», «Lista de promociones») llevan un ícono del mismo juego, y los cuadros de búsqueda una lupa.

## 7. Inventario de imágenes

### 7.1 Íconos, encabezados, tarjetas y fotos de mesas

La columna *Control* indica la función y el nombre interno del control. Los íconos marcados como predeterminado distinto de «la del diseño» se corrigieron porque no correspondían a su función (por ejemplo, el filtro Bebidas mostraba una moto de reparto).

| Ventana | Control (función) | Nombre del archivo | Ruta | Dimensión recomendada | Imagen predeterminada |
|---|---|---|---|---|---|
| Todas las ventanas de módulo | Menú lateral: Inicio · `pictureBox1` | `inicio.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Mesas · `pictureBox2` | `mesas.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Pedidos · `pictureBox3` | `pedidos.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Cocina · `pictureBox6` | `cocina.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Clientes · `pictureBox5` | `clientes.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Empleados · `pictureBox4` | `empleados.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Menú · `pictureBox10` | `menu.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Inventario · `pictureBox9` | `inventario.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Promociones · `pictureBox8` | `promociones.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Reportes · `pictureBox7` | `reportes.png` | `Imagenes\Iconos\` | 78 × 88 px | la del diseño |
| Todas las ventanas de módulo | Menú lateral: Cerrar sesión · `guna2PictureCerrar` | `cerrar_sesion.png` | `Imagenes\Iconos\` | 82 × 114 px | la del diseño |
| Todas las ventanas de módulo | Barra superior: Sistema de gestión · `guna2PictureBox7` | `sistema.png` | `Imagenes\Iconos\` | 84 × 84 px | la del diseño |
| Todas las ventanas de módulo | Barra superior: usuario de la sesión · `guna2PictureBox6` | `usuario.png` | `Imagenes\Iconos\` | 74 × 84 px | la del diseño |
| Todas las ventanas de módulo | Barra superior: fecha y hora · `guna2PictureBox10` | `reloj.png` | `Imagenes\Iconos\` | 74 × 84 px | la del diseño |
| Inicio | Inicio: banner superior · `guna2PictureBox1` | `banner_inicio.png` | `Imagenes\Paneles\` | 2414 × 418 px | la del diseño |
| Inicio | Inicio: tarjeta Ventas totales · `guna2PictureBox4` | `inicio_ventas.png` | `Imagenes\Paneles\` | 140 × 184 px | gráfico de ventas (antes tenía una mesa) |
| Inicio | Inicio: tarjeta Pedidos pendientes · `guna2PictureBox2` | `inicio_pendientes.png` | `Imagenes\Paneles\` | 120 × 136 px | lista con reloj (antes tenía un gráfico) |
| Inicio | Inicio: tarjeta Mesas ocupadas · `guna2PictureBox3` | `inicio_mesas_ocupadas.png` | `Imagenes\Paneles\` | 162 × 180 px | mesa (antes tenía una lista con reloj) |
| Inicio | Inicio: tarjeta Pedidos cobrados · `guna2PictureBox5` | `inicio_cobrados.png` | `Imagenes\Paneles\` | 168 × 180 px | recibo cobrado (antes tenía una caja) |
| Mesas | Mesas: ícono del encabezado · `guna2PictureBox1` | `encabezado_mesas.png` | `Imagenes\Paneles\` | 142 × 144 px | mesa (antes: el de Pedidos) |
| Mesas | Mesas: tarjeta Mesas totales · `guna2PictureBox2` | `mesas_totales.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Mesas | Mesas: tarjeta Mesas libres · `guna2PictureBox3` | `mesas_libres.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Mesas | Mesas: tarjeta Mesas ocupadas · `guna2PictureBox4` | `mesas_ocupadas.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Mesas | Mesas: tarjeta Mesas reservadas · `guna2PictureBox5` | `mesas_reservadas.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Mesas | Mesas: foto de cada mesa y del detalle · `11 controles (guna2PictureBox12 … guna2PictureBox22)` | `mesa.png` | `Imagenes\Paneles\` | 348 × 240 px | la del diseño |
| Mesas | Mesas: buscar · `guna2PictureBox11` | `buscar.png` | `Imagenes\Botones\` | 76 × 88 px | la del diseño |
| Pedidos | Pedidos: ícono del encabezado · `guna2PictureBox1` | `encabezado_pedidos.png` | `Imagenes\Paneles\` | 142 × 144 px | la del diseño |
| Pedidos | Filtro Platos · `guna2PictureBox28` | `categoria_platos.png` | `Imagenes\Iconos\` | 116 × 74 px | la del diseño |
| Pedidos | Filtro Agregados · `guna2PictureBox29` | `categoria_agregados.png` | `Imagenes\Iconos\` | 116 × 74 px | la del diseño |
| Pedidos | Filtro Bebidas · `guna2PictureBox30` | `categoria_bebidas.png` | `Imagenes\Iconos\` | 74 × 74 px | la del diseño |
| Pedidos | Tipo de pedido / filtro: Salón · `guna2PictureBox25` | `tipo_salon.png` | `Imagenes\Iconos\` | 138 × 114 px | la del diseño |
| Pedidos | Tipo de pedido / filtro: Delivery · `guna2PictureBox26` | `tipo_delivery.png` | `Imagenes\Iconos\` | 138 × 114 px | la del diseño |
| Pedidos | Tipo de pedido / filtro: Recojo · `guna2PictureBox27` | `tipo_recojo.png` | `Imagenes\Iconos\` | 138 × 114 px | la del diseño |
| Pedidos | Pedidos: botones de mesa (1 a 10) · `10 controles (guna2PictureBox31 … guna2PictureBox40)` | `mesa_pedido.png` | `Imagenes\Iconos\` | 96 × 74 px | la del diseño |
| Cocina | Cocina: ícono del encabezado · `guna2PictureBox1` | `encabezado_cocina.png` | `Imagenes\Paneles\` | 112 × 120 px | la del diseño |
| Cocina | Cocina: buscar · `guna2PictureBox11` | `buscar.png` | `Imagenes\Botones\` | 76 × 88 px | la del diseño |
| Cocina | Cocina: tarjeta Por iniciar · `guna2PictureBox3` | `cocina_por_iniciar.png` | `Imagenes\Paneles\` | 158 × 170 px | la del diseño |
| Cocina | Cocina: tarjeta En proceso · `guna2PictureBox2` | `cocina_en_proceso.png` | `Imagenes\Paneles\` | 156 × 170 px | la del diseño |
| Cocina | Cocina: tarjeta Listos para entregar · `guna2PictureBox4` | `cocina_listos.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Clientes | Clientes: ícono del encabezado · `guna2PictureBox1` | `encabezado_clientes.png` | `Imagenes\Paneles\` | 142 × 120 px | la del diseño |
| Clientes | Clientes: tarjeta Total de clientes · `guna2PictureBox12` | `clientes_total.png` | `Imagenes\Paneles\` | 162 × 112 px | la del diseño |
| Clientes | Clientes: tarjeta Clientes activos · `guna2PictureBox13` | `clientes_activos.png` | `Imagenes\Paneles\` | 158 × 110 px | la del diseño |
| Clientes | Clientes: tarjeta Clientes inactivos · `guna2PictureBox14` | `clientes_inactivos.png` | `Imagenes\Paneles\` | 136 × 112 px | la del diseño |
| Clientes | Clientes: tarjeta Clientes frecuentes · `guna2PictureBox15` | `clientes_frecuentes.png` | `Imagenes\Paneles\` | 134 × 110 px | la del diseño |
| Clientes | Clientes: foto del detalle del cliente · `guna2PictureBox16` | `cliente_foto.png` | `Imagenes\Paneles\` | 200 × 208 px | la del diseño |
| Empleados | Empleados: ícono del encabezado · `guna2PictureBox1` | `encabezado_empleados.png` | `Imagenes\Paneles\` | 142 × 144 px | la del diseño |
| Empleados | Empleados: buscar · `guna2PictureBox11` | `buscar.png` | `Imagenes\Botones\` | 76 × 88 px | la del diseño |
| Empleados | Empleados: tarjeta Total empleados · `guna2PictureBox2` | `empleados_total.png` | `Imagenes\Paneles\` | 140 × 136 px | la del diseño |
| Empleados | Empleados: tarjeta Administradores · `guna2PictureBox3` | `empleados_administradores.png` | `Imagenes\Paneles\` | 134 × 148 px | la del diseño |
| Empleados | Empleados: tarjeta Cocineros · `guna2PictureBox4` | `empleados_cocineros.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Empleados | Empleados: tarjeta Meseros · `guna2PictureBox5` | `empleados_meseros.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Empleados | Empleados: foto del detalle del empleado · `guna2PictureBox9` | `empleado_foto.png` | `Imagenes\Paneles\` | 192 × 212 px | la del diseño |
| Menú | Menú: ícono del encabezado · `guna2PictureBox1` | `encabezado_menu.png` | `Imagenes\Paneles\` | 142 × 144 px | carta del menú lateral (antes: el de Pedidos) |
| Menú | Menú: tarjeta Total productos · `guna2PictureBox37` | `menu_total.png` | `Imagenes\Paneles\` | 120 × 102 px | lista (ícono del sistema) |
| Menú | Menú: tarjeta Platos activos · `guna2PictureBox34` | `menu_platos.png` | `Imagenes\Paneles\` | 120 × 102 px | plato (ícono del sistema) |
| Menú | Menú: tarjeta Bebidas · `guna2PictureBox36` | `menu_bebidas.png` | `Imagenes\Paneles\` | 88 × 102 px | bebida (ícono del sistema) |
| Menú | Menú: tarjeta Promociones · `guna2PictureBox35` | `menu_promociones.png` | `Imagenes\Paneles\` | 120 × 102 px | etiqueta (ícono del sistema) |
| Inventario | Inventario: ícono del encabezado · `guna2PictureBox1` | `encabezado_inventario.png` | `Imagenes\Paneles\` | 142 × 144 px | almacén del menú lateral (antes: el de Pedidos) |
| Inventario | Inventario: tarjeta Total insumos · `guna2PictureBox37` | `inventario_total.png` | `Imagenes\Paneles\` | 120 × 102 px | caja (ícono del sistema) |
| Inventario | Inventario: tarjeta Stock adecuado · `guna2PictureBox34` | `inventario_stock_adecuado.png` | `Imagenes\Paneles\` | 120 × 102 px | visto (ícono del sistema) |
| Inventario | Inventario: tarjeta Stock bajo · `guna2PictureBox36` | `inventario_stock_bajo.png` | `Imagenes\Paneles\` | 120 × 102 px | alerta (ícono del sistema) |
| Inventario | Inventario: tarjeta Proveedores · `guna2PictureBox35` | `inventario_proveedores.png` | `Imagenes\Paneles\` | 120 × 102 px | bolsa (ícono del sistema) |
| Promociones | Promociones: ícono del encabezado · `guna2PictureBox1` | `encabezado_promociones.png` | `Imagenes\Paneles\` | 142 × 144 px | etiqueta (ícono del sistema) |
| Reportes | Reportes: ícono del encabezado · `guna2PictureBox1` | `encabezado_reportes.png` | `Imagenes\Paneles\` | 142 × 144 px | gráfico (ícono del sistema) |
| Reservar mesa | Reservar mesa: ícono del encabezado · `guna2PictureBox1` | `encabezado_reservas.png` | `Imagenes\Paneles\` | 100 × 112 px | calendario, como Nueva reserva (antes: recibo) |
| Generar recibo | Generar recibo: ícono del encabezado · `guna2PictureBox1` | `encabezado_recibo.png` | `Imagenes\Paneles\` | 144 × 140 px | la del diseño |

### 7.2 Íconos de botones

| Archivo | Ruta | Botones que lo usan | Dimensión recomendada | Imagen predeterminada |
|---|---|---|---|---|
| `actividad.png` | `Imagenes\Botones\` | Reportes «Ventas por canal» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\actividad.png`) |
| `banco.png` | `Imagenes\Botones\` | Reportes «Pagos mixtos» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\banco.png`) |
| `buscar.png` | `Imagenes\Botones\` | Clientes «Buscar», Generar recibo «Buscar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\buscar.png`) |
| `calcular.png` | `Imagenes\Botones\` | Generar recibo «Calcular» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\calcular.png`) |
| `calendario.png` | `Imagenes\Botones\` | Reportes «Esta semana», Reportes «Este mes», Reportes «Hoy», Reportes «Rango», Reportes «Reservas» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\calendario.png`) |
| `cancelar.png` | `Imagenes\Botones\` | Clientes «Cancelar», Generar recibo «Cancelar», Nueva reserva «Cancelar», Pedidos «Cancelar», Registro de empleado «Cancelar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\cancelar.png`) |
| `categoria_agregados.png` | `Imagenes\Botones\` | Inventario «Agregados», Menú «Agregados» | 96 × 96 px, PNG transparente | pictograma de Pedidos |
| `categoria_bebidas.png` | `Imagenes\Botones\` | Inventario «Bebidas», Menú «Bebidas» | 96 × 96 px, PNG transparente | pictograma de Pedidos |
| `categoria_platos.png` | `Imagenes\Botones\` | Inventario «Platos», Menú «Platos» | 96 × 96 px, PNG transparente | pictograma de Pedidos |
| `cobrar.png` | `Imagenes\Botones\` | Generar recibo «Cobrar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\cobrar.png`) |
| `desactivar.png` | `Imagenes\Botones\` | Promociones «Desactivar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\desactivar.png`) |
| `detalle.png` | `Imagenes\Botones\` | Mesas «Detalle pedido» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\detalle.png`) |
| `editar.png` | `Imagenes\Botones\` | Clientes «Editar», Inventario «Editar», Menú «Editar», Promociones «Editar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\editar.png`) |
| `eliminar.png` | `Imagenes\Botones\` | Clientes «Eliminar», Inventario «Eliminar», Menú «Eliminar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\eliminar.png`) |
| `entrada.png` | `Imagenes\Botones\` | Inventario «Registrar entrada» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\entrada.png`) |
| `etiqueta.png` | `Imagenes\Botones\` | Menú «Gestionar», Reportes «Promociones y descuentos» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\etiqueta.png`) |
| `excel.png` | `Imagenes\Botones\` | Reportes «Excel» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\excel.png`) |
| `generar.png` | `Imagenes\Botones\` | Reportes «Generar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\generar.png`) |
| `grafico.png` | `Imagenes\Botones\` | Reportes «Ventas por período» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\grafico.png`) |
| `guardar.png` | `Imagenes\Botones\` | Clientes «Guardar», Inventario «Guardar», Menú «Guardar», Nueva reserva «Guardar», Pedidos «Guardar», Promociones «Guardar», Registro de empleado «Guardar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\guardar.png`) |
| `historial.png` | `Imagenes\Botones\` | Promociones «Vencidas» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\historial.png`) |
| `imagen.png` | `Imagenes\Botones\` | Inventario «Cambiar imagen», Menú «Cambiar imagen» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\imagen.png`) |
| `imprimir.png` | `Imagenes\Botones\` | Generar recibo «Imprimir», Pedidos «Reimprimir recibo» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\imprimir.png`) |
| `inactivo.png` | `Imagenes\Botones\` | Promociones «Inactivas» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\inactivo.png`) |
| `liberar.png` | `Imagenes\Botones\` | Mesas «Liberar mesa» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\liberar.png`) |
| `limpiar.png` | `Imagenes\Botones\` | Clientes «Limpiar», Pedidos «Limpiar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\limpiar.png`) |
| `lista.png` | `Imagenes\Botones\` | Mesas «Todas», Promociones «Todas» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\lista.png`) |
| `nueva_reserva.png` | `Imagenes\Botones\` | Mesas «Nueva reserva» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\nueva_reserva.png`) |
| `nuevo.png` | `Imagenes\Botones\` | Clientes «Nuevo cliente», Empleados «Nuevo empleado», Inventario «Nuevo insumo», Menú «Nuevo producto», Pedidos «NUEVO PEDIDO», Promociones «Nueva promoción» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\nuevo.png`) |
| `pdf.png` | `Imagenes\Botones\` | Reportes «PDF» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\pdf.png`) |
| `persona.png` | `Imagenes\Botones\` | Mesas «Ocupada», Reportes «Cobros por empleado» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\persona.png`) |
| `personas.png` | `Imagenes\Botones\` | Reportes «Clientes frecuentes» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\personas.png`) |
| `plato.png` | `Imagenes\Botones\` | Reportes «Productos más vendidos» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\plato.png`) |
| `recibo.png` | `Imagenes\Botones\` | Pedidos «Generar recibo» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\recibo.png`) |
| `reloj.png` | `Imagenes\Botones\` | Promociones «Programadas» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\reloj.png`) |
| `reservar.png` | `Imagenes\Botones\` | Mesas «Reservada», Mesas «Reservar» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\reservar.png`) |
| `salida.png` | `Imagenes\Botones\` | Inventario «Registrar salida» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\salida.png`) |
| `tarjeta.png` | `Imagenes\Botones\` | Reportes «Ingresos por método de pago» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\tarjeta.png`) |
| `tipo_delivery.png` | `Imagenes\Botones\` | Cocina «Delivery» | 96 × 96 px, PNG transparente | pictograma de Pedidos |
| `tipo_recojo.png` | `Imagenes\Botones\` | Cocina «Recojo» | 96 × 96 px, PNG transparente | pictograma de Pedidos |
| `tipo_salon.png` | `Imagenes\Botones\` | Cocina «Salon» | 96 × 96 px, PNG transparente | pictograma de Pedidos |
| `todos.png` | `Imagenes\Botones\` | Cocina «Todos», Inventario «Todos», Menú «Todos» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\todos.png`) |
| `vista_previa.png` | `Imagenes\Botones\` | Generar recibo «Vista previa» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\vista_previa.png`) |
| `visto.png` | `Imagenes\Botones\` | Mesas «Libres», Promociones «Vigentes», Reportes «Pedidos por estado» | 96 × 96 px, PNG transparente | ícono del sistema (`Resources\Iconos\visto.png`) |

### 7.3 Productos

| id_plato | Producto | Nombre del archivo | Ruta | Dimensión recomendada | Imagen predeterminada |
|---|---|---|---|---|---|
| 1 | Ceviche clásico | `plato_1.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 2 | Ceviche mixto | `plato_2.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 3 | Tiradito al ají amarillo | `plato_3.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 5 | Chicharrón de calamar | `plato_5.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 6 | Chicha morada 1 L (inactivo) | `plato_6.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 7 | Leche asada (inactivo) | `plato_7.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 11 | Alitas BBQ (5 alitas) | `plato_11.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 12 | Alitas Acebichada (5 alitas) | `plato_12.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 13 | Alitas BBQ (4 alitas) | `plato_13.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 14 | Alitas Acebichada (4 alitas) | `plato_14.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 15 | Salchialitas | `plato_15.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 16 | Lomo de carne | `plato_16.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 17 | Lomo de pollo | `plato_17.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 18 | Mostrito | `plato_18.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 19 | Milanesa de pollo | `plato_19.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 20 | Tallarín verde | `plato_20.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 21 | Tallarín saltado | `plato_21.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 22 | Huevo | `plato_22.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 23 | Plátano | `plato_23.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 24 | Jarra de maracuyá 1 litro | `plato_24.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 25 | Jarra de maracuyá 1/2 litro | `plato_25.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 26 | Coca-Cola personal | `plato_26.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 27 | Inca Kola personal | `plato_27.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 28 | Sprite personal | `plato_28.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| 29 | Fanta personal | `plato_29.jpg` | `Imagenes\Productos\` | 600 × 400 px | foto incluida en el programa |
| — | Productos sin ninguna foto | `predeterminado.png` | `Imagenes\Productos\` | 600 × 400 px | recuadro «Sin foto» |
