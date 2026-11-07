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

## 6. Inventario de imágenes

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
| Mesas | Mesas: botón Nueva reserva · `guna2PictureBox9` | `nueva_reserva.png` | `Imagenes\Botones\` | 80 × 76 px | la del diseño |
| Mesas | Mesas: buscar · `guna2PictureBox11` | `buscar.png` | `Imagenes\Botones\` | 76 × 88 px | la del diseño |
| Mesas | Mesas: botón Detalle pedido · `guna2PictureBox23` | `detalle_pedido.png` | `Imagenes\Botones\` | 86 × 84 px | la del diseño |
| Mesas | Mesas: botón Reservar · `guna2PictureBox24` | `reservar.png` | `Imagenes\Botones\` | 86 × 84 px | la del diseño |
| Mesas | Mesas: botón Liberar mesa · `guna2PictureBox25` | `liberar_mesa.png` | `Imagenes\Botones\` | 86 × 84 px | la del diseño |
| Pedidos | Pedidos: ícono del encabezado · `guna2PictureBox1` | `encabezado_pedidos.png` | `Imagenes\Paneles\` | 142 × 144 px | la del diseño |
| Pedidos | Pedidos: botón Nuevo pedido · `guna2PictureBox44` | `nuevo_pedido.png` | `Imagenes\Botones\` | 64 × 74 px | la del diseño |
| Pedidos | Pedidos: botón Guardar · `guna2PictureBox42` | `guardar.png` | `Imagenes\Botones\` | 96 × 78 px | disquete (antes: el de Generar recibo) |
| Pedidos | Pedidos: botón Limpiar · `guna2PictureBox41` | `limpiar.png` | `Imagenes\Botones\` | 52 × 58 px | escoba, como en Clientes |
| Pedidos | Pedidos: botón Cancelar · `guna2PictureBox43` | `cancelar.png` | `Imagenes\Botones\` | 60 × 56 px | la del diseño |
| Pedidos | Pedidos: botón Generar recibo · `guna2PictureBox45` | `generar_recibo.png` | `Imagenes\Botones\` | 96 × 78 px | la del diseño |
| Pedidos | Filtro Platos · `guna2PictureBox28` | `categoria_platos.png` | `Imagenes\Iconos\` | 116 × 74 px | la del diseño |
| Pedidos | Filtro Agregados · `guna2PictureBox29` | `categoria_agregados.png` | `Imagenes\Iconos\` | 116 × 74 px | la del diseño |
| Pedidos | Filtro Bebidas · `guna2PictureBox30` | `categoria_bebidas.png` | `Imagenes\Iconos\` | 74 × 74 px | la del diseño |
| Pedidos | Tipo de pedido / filtro: Salón · `guna2PictureBox25` | `tipo_salon.png` | `Imagenes\Iconos\` | 138 × 114 px | la del diseño |
| Pedidos | Tipo de pedido / filtro: Delivery · `guna2PictureBox26` | `tipo_delivery.png` | `Imagenes\Iconos\` | 138 × 114 px | la del diseño |
| Pedidos | Tipo de pedido / filtro: Recojo · `guna2PictureBox27` | `tipo_recojo.png` | `Imagenes\Iconos\` | 138 × 114 px | la del diseño |
| Pedidos | Pedidos: botones de mesa (1 a 10) · `10 controles (guna2PictureBox31 … guna2PictureBox40)` | `mesa_pedido.png` | `Imagenes\Iconos\` | 96 × 74 px | la del diseño |
| Cocina | Cocina: ícono del encabezado · `guna2PictureBox1` | `encabezado_cocina.png` | `Imagenes\Paneles\` | 112 × 120 px | la del diseño |
| Cocina | Filtro Todos · `guna2PictureBox28` | `filtro_todos.png` | `Imagenes\Iconos\` | 84 × 62 px | la del diseño |
| Cocina | Tipo de pedido / filtro: Salón · `guna2PictureBox29` | `tipo_salon.png` | `Imagenes\Iconos\` | 84 × 62 px | la del diseño |
| Cocina | Tipo de pedido / filtro: Delivery · `guna2PictureBox30` | `tipo_delivery.png` | `Imagenes\Iconos\` | 84 × 62 px | la del diseño |
| Cocina | Tipo de pedido / filtro: Recojo · `guna2PictureBox31` | `tipo_recojo.png` | `Imagenes\Iconos\` | 84 × 62 px | la del diseño |
| Cocina | Cocina: buscar · `guna2PictureBox11` | `buscar.png` | `Imagenes\Botones\` | 76 × 88 px | la del diseño |
| Cocina | Cocina: tarjeta Por iniciar · `guna2PictureBox3` | `cocina_por_iniciar.png` | `Imagenes\Paneles\` | 158 × 170 px | la del diseño |
| Cocina | Cocina: tarjeta En proceso · `guna2PictureBox2` | `cocina_en_proceso.png` | `Imagenes\Paneles\` | 156 × 170 px | la del diseño |
| Cocina | Cocina: tarjeta Listos para entregar · `guna2PictureBox4` | `cocina_listos.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Clientes | Clientes: ícono del encabezado · `guna2PictureBox1` | `encabezado_clientes.png` | `Imagenes\Paneles\` | 142 × 120 px | la del diseño |
| Clientes | Botón Nuevo (cliente) · `guna2PictureBox30` | `nuevo.png` | `Imagenes\Botones\` | 84 × 62 px | la del diseño |
| Clientes | Botón Editar · `guna2PictureBox2` | `editar.png` | `Imagenes\Botones\` | 84 × 62 px | la del diseño |
| Clientes | Botón Eliminar · `guna2PictureBox3` | `eliminar.png` | `Imagenes\Botones\` | 84 × 62 px | la del diseño |
| Clientes | Botón Limpiar · `guna2PictureBox4` | `limpiar.png` | `Imagenes\Botones\` | 84 × 62 px | la del diseño |
| Clientes | Botón Buscar · `guna2PictureBox5` | `buscar.png` | `Imagenes\Botones\` | 68 × 62 px | la del diseño |
| Clientes | Botón Guardar · `guna2PictureBox11` | `guardar.png` | `Imagenes\Botones\` | 106 × 76 px | la del diseño |
| Clientes | Botón Cancelar · `guna2PictureBox9` | `cancelar.png` | `Imagenes\Botones\` | 94 × 76 px | la del diseño |
| Clientes | Clientes: tarjeta Total de clientes · `guna2PictureBox12` | `clientes_total.png` | `Imagenes\Paneles\` | 162 × 112 px | la del diseño |
| Clientes | Clientes: tarjeta Clientes activos · `guna2PictureBox13` | `clientes_activos.png` | `Imagenes\Paneles\` | 158 × 110 px | la del diseño |
| Clientes | Clientes: tarjeta Clientes inactivos · `guna2PictureBox14` | `clientes_inactivos.png` | `Imagenes\Paneles\` | 136 × 112 px | la del diseño |
| Clientes | Clientes: tarjeta Clientes frecuentes · `guna2PictureBox15` | `clientes_frecuentes.png` | `Imagenes\Paneles\` | 134 × 110 px | la del diseño |
| Clientes | Clientes: foto del detalle del cliente · `guna2PictureBox16` | `cliente_foto.png` | `Imagenes\Paneles\` | 200 × 208 px | la del diseño |
| Empleados | Empleados: ícono del encabezado · `guna2PictureBox1` | `encabezado_empleados.png` | `Imagenes\Paneles\` | 142 × 144 px | la del diseño |
| Empleados | Botón Nuevo (empleado) · `guna2PictureBox30` | `nuevo.png` | `Imagenes\Botones\` | 84 × 60 px | la del diseño |
| Empleados | Empleados: buscar · `guna2PictureBox11` | `buscar.png` | `Imagenes\Botones\` | 76 × 88 px | la del diseño |
| Empleados | Empleados: tarjeta Total empleados · `guna2PictureBox2` | `empleados_total.png` | `Imagenes\Paneles\` | 140 × 136 px | la del diseño |
| Empleados | Empleados: tarjeta Administradores · `guna2PictureBox3` | `empleados_administradores.png` | `Imagenes\Paneles\` | 134 × 148 px | la del diseño |
| Empleados | Empleados: tarjeta Cocineros · `guna2PictureBox4` | `empleados_cocineros.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Empleados | Empleados: tarjeta Meseros · `guna2PictureBox5` | `empleados_meseros.png` | `Imagenes\Paneles\` | 120 × 136 px | la del diseño |
| Empleados | Empleados: foto del detalle del empleado · `guna2PictureBox9` | `empleado_foto.png` | `Imagenes\Paneles\` | 192 × 212 px | la del diseño |
| Menú | Menú: ícono del encabezado · `guna2PictureBox1` | `encabezado_menu.png` | `Imagenes\Paneles\` | 142 × 144 px | carta del menú lateral (antes: el de Pedidos) |
| Menú | Menú: botón Nuevo producto · `guna2PictureBox28` | `nuevo_producto.png` | `Imagenes\Botones\` | 82 × 76 px | la del diseño |
| Menú | Filtro Todos · `guna2PictureBox27` | `filtro_todos.png` | `Imagenes\Iconos\` | 84 × 62 px | la del diseño |
| Menú | Filtro Platos · `guna2PictureBox26` | `categoria_platos.png` | `Imagenes\Iconos\` | 84 × 62 px | plato, como en Pedidos (antes: mesa) |
| Menú | Filtro Bebidas · `guna2PictureBox25` | `categoria_bebidas.png` | `Imagenes\Iconos\` | 84 × 62 px | bebida, como en Pedidos (antes: moto) |
| Menú | Filtro Agregados · `guna2PictureBox31` | `categoria_agregados.png` | `Imagenes\Iconos\` | 84 × 62 px | agregados, como en Pedidos (antes: bolsa) |
| Menú | Botón Guardar · `guna2PictureBox30` | `guardar.png` | `Imagenes\Botones\` | 80 × 88 px | la del diseño |
| Menú | Botón Editar · `guna2PictureBox32` | `editar.png` | `Imagenes\Botones\` | 84 × 88 px | la del diseño |
| Menú | Botón Eliminar · `guna2PictureBox33` | `eliminar.png` | `Imagenes\Botones\` | 84 × 88 px | la del diseño |
| Menú | Menú: tarjeta Total productos · `guna2PictureBox37` | `menu_total.png` | `Imagenes\Paneles\` | 120 × 102 px | la del diseño |
| Menú | Menú: tarjeta Platos activos · `guna2PictureBox34` | `menu_platos.png` | `Imagenes\Paneles\` | 120 × 102 px | la del diseño |
| Menú | Menú: tarjeta Bebidas · `guna2PictureBox36` | `menu_bebidas.png` | `Imagenes\Paneles\` | 88 × 102 px | la del diseño |
| Menú | Menú: tarjeta Promociones · `guna2PictureBox35` | `menu_promociones.png` | `Imagenes\Paneles\` | 120 × 102 px | la del diseño |
| Inventario | Inventario: ícono del encabezado · `guna2PictureBox1` | `encabezado_inventario.png` | `Imagenes\Paneles\` | 142 × 144 px | almacén del menú lateral (antes: el de Pedidos) |
| Inventario | Inventario: botón Nuevo insumo · `guna2PictureBox28` | `nuevo.png` | `Imagenes\Botones\` | 116 × 62 px | documento con +, como Nuevo pedido (antes: bolsa) |
| Inventario | Filtro Todos · `guna2PictureBox27` | `filtro_todos.png` | `Imagenes\Iconos\` | 84 × 62 px | la del diseño |
| Inventario | Filtro Platos · `guna2PictureBox26` | `categoria_platos.png` | `Imagenes\Iconos\` | 84 × 62 px | plato, como en Pedidos (antes: mesa) |
| Inventario | Filtro Bebidas · `guna2PictureBox25` | `categoria_bebidas.png` | `Imagenes\Iconos\` | 84 × 62 px | bebida, como en Pedidos (antes: moto) |
| Inventario | Filtro Agregados · `guna2PictureBox31` | `categoria_agregados.png` | `Imagenes\Iconos\` | 84 × 62 px | agregados, como en Pedidos (antes: bolsa) |
| Inventario | Inventario: botón Registrar entrada · `guna2PictureBox30` | `registrar_entrada.png` | `Imagenes\Botones\` | 92 × 62 px | flecha de ingreso (antes: bolsa) |
| Inventario | Inventario: botón Registrar salida · `guna2PictureBox29` | `registrar_salida.png` | `Imagenes\Botones\` | 92 × 62 px | flecha de salida (antes: bolsa) |
| Inventario | Botón Guardar · `guna2PictureBox38` | `guardar.png` | `Imagenes\Botones\` | 56 × 64 px | la del diseño |
| Inventario | Inventario: botón Editar · `guna2Button16` | `editar.png` | `Imagenes\Botones\` | 218 × 102 px | lápiz (antes sin ícono) |
| Inventario | Inventario: botón Eliminar · `guna2Button17` | `eliminar.png` | `Imagenes\Botones\` | 272 × 102 px | papelera (antes sin ícono) |
| Inventario | Inventario: tarjeta Total insumos · `guna2PictureBox37` | `inventario_total.png` | `Imagenes\Paneles\` | 120 × 102 px | la del diseño |
| Inventario | Inventario: tarjeta Stock adecuado · `guna2PictureBox34` | `inventario_stock_adecuado.png` | `Imagenes\Paneles\` | 120 × 102 px | la del diseño |
| Inventario | Inventario: tarjeta Stock bajo · `guna2PictureBox36` | `inventario_stock_bajo.png` | `Imagenes\Paneles\` | 120 × 102 px | la del diseño |
| Inventario | Inventario: tarjeta Proveedores · `guna2PictureBox35` | `inventario_proveedores.png` | `Imagenes\Paneles\` | 120 × 102 px | la del diseño |
| Promociones | Promociones: ícono del encabezado · `guna2PictureBox1` | `encabezado_promociones.png` | `Imagenes\Paneles\` | 142 × 144 px | la del diseño |
| Reportes | Reportes: ícono del encabezado · `guna2PictureBox1` | `encabezado_reportes.png` | `Imagenes\Paneles\` | 142 × 144 px | la del diseño |
| Reservar mesa | Reservar mesa: ícono del encabezado · `guna2PictureBox1` | `encabezado_reservas.png` | `Imagenes\Paneles\` | 100 × 112 px | calendario, como Nueva reserva (antes: recibo) |
| Reservar mesa | Reservar mesa: botón Guardar · `guna2PictureBox43` | `guardar.png` | `Imagenes\Botones\` | 60 × 56 px | disquete (antes: una X) |
| Reservar mesa | Reservar mesa: botón Cancelar · `guna2PictureBox2` | `cancelar.png` | `Imagenes\Botones\` | 60 × 56 px | la del diseño |
| Generar recibo | Generar recibo: ícono del encabezado · `guna2PictureBox1` | `encabezado_recibo.png` | `Imagenes\Paneles\` | 144 × 140 px | la del diseño |
| Generar recibo | Generar recibo: botón Vista previa · `btnVistaPreviaBoleta` | `vista_previa.png` | `Imagenes\Botones\` | 322 × 116 px | la del diseño |
| Generar recibo | Generar recibo: botón Imprimir · `btnImprimirBoleta` | `imprimir.png` | `Imagenes\Botones\` | 322 × 116 px | la del diseño |
| Generar recibo | Generar recibo: botón Cancelar · `btnCancelarBoleta` | `cancelar.png` | `Imagenes\Botones\` | 322 × 116 px | la del diseño |
| Generar recibo | Generar recibo: botón Cobrar · `btnRegistrarCobro` | `cobrar.png` | `Imagenes\Botones\` | 322 × 116 px | la del diseño |

### Productos

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
