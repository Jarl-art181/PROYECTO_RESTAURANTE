/* =====================================================================
   RESTABLECER LA CONTRASEÑA DE UNA CUENTA (por ejemplo, el Administrador)
   ---------------------------------------------------------------------
   Úselo SOLO si nadie conoce la contraseña de la cuenta.
   Requiere permisos de administrador de SQL Server (autenticación de
   Windows del equipo servidor): esa es la protección del procedimiento.

   * La clave temporal la elige usted al ejecutar el script; NO queda
     escrita en el proyecto ni en este archivo.
   * La cuenta queda habilitada, desbloqueada y con
     debe_cambiar_contrasena = 1: al entrar, el sistema obliga a elegir
     una contraseña nueva (que se guarda en formato PBKDF2).
   * Solo funciona con empleados ACTIVOS que ya tienen usuario.

   Ejemplo (en una sola línea, cambiando la clave temporal):
     sqlcmd -S LAPTOP-GH63852S\SQLEXPRESS -E -C -d AL_ESTILO_PIURA -I -f 65001
            -v USUARIO="aruiz" CLAVE_TEMPORAL="Temporal2026x"
            -i RESTABLECER_CONTRASENA_TEMPORAL.sql
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Usuario VARCHAR(50) = '$(USUARIO)';
DECLARE @Clave   NVARCHAR(128) = N'$(CLAVE_TEMPORAL)';

IF LEN(@Clave) < 8
    THROW 54001, 'La clave temporal debe tener al menos 8 caracteres.', 1;

BEGIN TRANSACTION;

-- Mismo formato que acepta la aplicación (SHA-256 en hexadecimal, UTF-8).
UPDATE dbo.EMPLEADO
   SET [contraseña] = CONVERT(VARCHAR(64),
                          HASHBYTES('SHA2_256',
                              CONVERT(VARCHAR(512), @Clave COLLATE Latin1_General_100_CI_AS_SC_UTF8)), 2),
       debe_cambiar_contrasena = 1,
       acceso_habilitado = 1,
       intentos_fallidos = 0,
       bloqueo_hasta = NULL
 WHERE usuario = @Usuario
   AND estado = 'activo';

IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 54002, 'No existe un empleado ACTIVO con ese usuario. No se cambió nada.', 1;
END

COMMIT TRANSACTION;
PRINT 'Contraseña temporal asignada. Al iniciar sesión se pedirá cambiarla.';
