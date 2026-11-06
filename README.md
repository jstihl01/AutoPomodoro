# AutoPomodoro para Windows 11

[![Descargar AutoPomodoro para Windows](https://img.shields.io/badge/DESCARGAR-AutoPomodoro%20para%20Windows-1673C9?style=for-the-badge)](https://github.com/jstihl01/AutoPomodoro/releases/download/v2.0.1/AutoPomodoro-Setup-v2.0.1.exe)

Temporizador Pomodoro de escritorio sincronizado con la hora real. AutoPomodoro permite trabajar con intervalos 25/5 o 50/10 sin iniciar, pausar ni reajustar manualmente el reloj.

El icono propio de AutoPomodoro se integra en la aplicacion, la barra de tareas, los accesos directos y el instalador.

## Funcionamiento

- Modo 25/5: trabajo de `:00` a `:25` y de `:30` a `:55`; descanso durante los cinco minutos restantes de cada media hora.
- Modo 50/10: trabajo de `:00` a `:50`; descanso durante los diez minutos restantes de cada hora.
- Colores planos: verde durante el trabajo 25/5, rojo durante el trabajo 50/10 y azul durante el descanso.
- Contador monoespaciado Consolas con ancho estable en ventana y widget.
- Siempre visible sobre las ventanas normales, redimensionable y sin barra superior ni boton de cierre.
- Mientras esta abierta, mantiene la pantalla encendida y evita la suspension por inactividad para que los pitidos puedan escucharse.
- Menu de ajustes para elegir modo y volumen entre 0% y 100%, con prueba sonora.
- Boton para restablecer volumen, modo, tamano y posicion a sus valores predeterminados.
- Un tono suave al comenzar el descanso y tres tonos al volver al trabajo.
- Una sola instancia y ninguna ejecucion en segundo plano al cerrar.
- Recuerda tamano, posicion, modo y volumen en `%LocalAppData%\AutoPomodoro\settings.json` y migra automaticamente los ajustes de versiones anteriores.

La ventana se cierra desde su icono en la barra de tareas o con `Alt+F4`. El menu se cierra con el boton `LISTO` o la tecla `Esc`; al cerrarlo guarda los cambios.

## Instalar

### Widget en barra de tareas

El boton de minimizar de la ventana cambia a un contador dentro de una capsula
negra redondeada junto al area de notificacion del monitor principal. El texto es
verde para trabajo 25/5, rojo para trabajo 50/10 y azul durante el descanso.
Pasar el cursor por el altavoz abre el volumen de AutoPomodoro con porcentaje;
hacer clic silencia o restaura el ultimo volumen audible.

El clic izquierdo en los numeros recupera la ventana. El engranaje abre los
ajustes de modo y volumen, con botones y deslizadores redondeados.
El altavoz esta a la izquierda e indica silencio o volumen bajo, medio y alto.
El widget no tiene menu de clic derecho. Se recuerda la vista seleccionada.
Volver a abrir AutoPomodoro desde su acceso directo recupera siempre la ventana.
Los sonidos y la prevencion de suspension siguen funcionando en ambas vistas.

Esta vista usa una ventana WPF vinculada a Explorer mediante Win32; no es un
widget del panel Win+W y no reserva espacio ni desplaza los iconos de Windows.
Solo admite la barra horizontal principal y puede solaparse con iconos si no
hay espacio disponible. Se oculta con la barra y ante ventanas a pantalla
completa; si no encuentra una barra compatible vuelve a la ventana normal.
El modo ventana permanece disponible como alternativa ante cambios de Windows.

Descarga el instalador con el botón del principio y ejecútalo para instalar o actualizar AutoPomodoro. Crea accesos en el escritorio y en el menú Inicio y no necesita permisos de administrador.

El instalador no esta firmado porque el proyecto no dispone de un certificado de firma de codigo. Windows SmartScreen puede mostrar una advertencia. Puedes comprobar el archivo descargado con el checksum de [GitHub Releases](https://github.com/jstihl01/AutoPomodoro/releases/latest):

```powershell
Get-FileHash .\AutoPomodoro-Setup-v2.0.1.exe -Algorithm SHA256
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
