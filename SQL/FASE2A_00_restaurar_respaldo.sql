/* =====================================================================
   RESTAURAR EL RESPALDO TOMADO ANTES DE LA FASE 2A
   Respaldo: AL_ESTILO_PIURA_antes_FASE2A_20261105.bak
   (BACKUP ... WITH CHECKSUM; verificado con RESTORE VERIFYONLY y con
   una restauración de prueba en una base temporal: 33 tablas, mismas filas)

   ¡ATENCIÓN! Reemplaza la base actual por la del 05/11/2026 17:17.
   Se pierde todo lo registrado después (pedidos, cobros, empleados).
   Cierre la aplicación y Visual Studio antes de ejecutarlo.

     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -d master -i FASE2A_00_restaurar_respaldo.sql
   ===================================================================== */

USE master;

ALTER DATABASE [AL_ESTILO_PIURA] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

RESTORE DATABASE [AL_ESTILO_PIURA]
FROM DISK = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup\AL_ESTILO_PIURA_antes_FASE2A_20261105.bak'
WITH REPLACE, CHECKSUM;

ALTER DATABASE [AL_ESTILO_PIURA] SET MULTI_USER;
