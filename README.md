# AutoPomodoro para Windows 11

Temporizador Pomodoro de escritorio sincronizado con la hora real. AutoPomodoro permite trabajar con intervalos 25/5 o 50/10 sin iniciar, pausar ni reajustar manualmente el reloj.

El icono propio de AutoPomodoro se integra en la aplicacion, la barra de tareas, los accesos directos y el instalador.

## Funcionamiento

- Modo 25/5: trabajo de `:00` a `:25` y de `:30` a `:55`; descanso durante los cinco minutos restantes de cada media hora.
- Modo 50/10: trabajo de `:00` a `:50`; descanso durante los diez minutos restantes de cada hora.
- Fondo verde durante el trabajo 25/5, rojo durante el trabajo 50/10 y azul durante el descanso.
- Siempre visible sobre las ventanas normales, redimensionable y sin barra superior ni boton de cierre.
- Mientras esta abierta, mantiene la pantalla encendida y evita la suspension por inactividad para que los pitidos puedan escucharse.
- Menu de ajustes para elegir modo y volumen entre 0% y 100%, con prueba sonora.
- Boton para restablecer volumen, modo, tamano y posicion a sus valores predeterminados.
- Un tono suave al comenzar el descanso y tres tonos al volver al trabajo.
- Una sola instancia y ninguna ejecucion en segundo plano al cerrar.
- Recuerda tamano, posicion, modo y volumen en `%LocalAppData%\AutoPomodoro\settings.json` y migra automaticamente los ajustes de versiones anteriores.

La ventana se cierra desde su icono en la barra de tareas o con `Alt+F4`. El menu se cierra con el boton `LISTO` o la tecla `Esc`; al cerrarlo guarda los cambios.

## Instalar

### Vista experimental en barra de tareas (1.5.0 beta)

Descarga la beta desde [GitHub Releases](https://github.com/jstihl01/AutoPomodoro/releases/tag/v1.5.0-beta.1)
e instala `AutoPomodoro-Setup-v1.5.0-beta.1.exe` sobre la version anterior.

En Ajustes, el boton `MOSTRAR EN BARRA (EXPERIMENTAL)` cambia a un contador
transparente junto al area de notificacion del monitor principal. El texto es
verde para trabajo 25/5, rojo para trabajo 50/10 y azul durante el descanso.
Pasar el cursor por el altavoz abre el volumen de AutoPomodoro con porcentaje;
hacer clic silencia o restaura el ultimo volumen audible.

El clic derecho permite volver a la ventana, cambiar el modo, desplazar el
contador, abrir ajustes o salir. Se recuerdan la vista y el desplazamiento.
Volver a abrir AutoPomodoro desde su acceso directo recupera siempre la ventana.
Los sonidos y la prevencion de suspension siguen funcionando en ambas vistas.

Esta vista usa una ventana WPF vinculada a Explorer mediante Win32; no es un
widget del panel Win+W y no reserva espacio ni desplaza los iconos de Windows.
Puede ser necesario moverla con su menu para evitar solapamientos. Solo admite
la barra horizontal principal. Se oculta con la barra y ante ventanas a pantalla
completa; si no encuentra una barra compatible vuelve a la ventana normal.
La compatibilidad visual con Explorer, reinicios y distintas escalas de pantalla
requiere verificacion manual antes de publicar esta beta como version estable.

El instalador principal esta en `releases\AutoPomodoro-Setup-v1.4.0.exe`.

Ejecutalo para instalar AutoPomodoro. El instalador crea accesos en el escritorio y en el menu Inicio y no necesita permisos de administrador.

El instalador no esta firmado porque el proyecto no dispone de un certificado de firma de codigo. Windows SmartScreen puede mostrar una advertencia. El archivo `AutoPomodoro-Setup-v1.4.0.exe.sha256` permite comprobar que el instalador no ha cambiado:

```powershell
Get-FileHash .\releases\AutoPomodoro-Setup-v1.4.0.exe -Algorithm SHA256
```

Volver a ejecutar el instalador reemplaza limpiamente la aplicacion y conserva la posicion y el tamano. La desinstalacion elimina tambien esos ajustes.

## Compilar

Requisitos para desarrollo:

- SDK de .NET 8
- Inno Setup 6
- Windows 11 x64

```powershell
.\build.ps1
```

El script restaura y compila la solucion, ejecuta las pruebas, publica una aplicacion autocontenida y genera el instalador versionado y su checksum en `releases\`.

## Privacidad y limitaciones

La aplicacion no usa red, cuentas, telemetria ni bandeja del sistema. Mientras esta abierta solicita a Windows mantener el equipo despierto y la pantalla encendida, pero no puede impedir bloqueos manuales, politicas corporativas, bateria critica o suspension forzada. La opcion "siempre visible" se aplica al escritorio normal de Windows; las pantallas seguras del sistema y algunas aplicaciones a pantalla completa pueden mostrarse por encima.
