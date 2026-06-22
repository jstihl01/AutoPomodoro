# Pomodoro para Windows 11

Temporizador Pomodoro de escritorio sincronizado con la hora real. Permite trabajar con intervalos 25/5 o 50/10 sin iniciar, pausar ni reajustar manualmente el reloj.

## Funcionamiento

- Modo 25/5: trabajo de `:00` a `:25` y de `:30` a `:55`; descanso durante los cinco minutos restantes de cada media hora.
- Modo 50/10: trabajo de `:00` a `:50`; descanso durante los diez minutos restantes de cada hora.
- Fondo verde durante el trabajo 25/5, rojo durante el trabajo 50/10 y azul durante el descanso.
- Siempre visible sobre las ventanas normales, redimensionable y sin barra superior ni boton de cierre.
- Menu de ajustes para elegir modo y volumen entre 0% y 100%, con prueba sonora.
- Un tono suave al comenzar el descanso y tres tonos al volver al trabajo.
- Una sola instancia y ninguna ejecucion en segundo plano al cerrar.
- Recuerda tamano, posicion, modo y volumen en `%LocalAppData%\Pomodoro\settings.json`.

La ventana se cierra desde su icono en la barra de tareas o con `Alt+F4`. El menu se cierra con el boton `LISTO` o la tecla `Esc`; al cerrarlo guarda los cambios.

## Instalar

Ejecuta `dist\Pomodoro-Setup.exe`. El instalador crea accesos en el escritorio y en el menu Inicio y no necesita permisos de administrador.

El instalador no esta firmado porque el proyecto no dispone de un certificado de firma de codigo. Windows SmartScreen puede mostrar una advertencia. El archivo `Pomodoro-Setup.exe.sha256` permite comprobar que el instalador no ha cambiado:

```powershell
Get-FileHash .\dist\Pomodoro-Setup.exe -Algorithm SHA256
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

El script restaura y compila la solucion, ejecuta las pruebas, publica una aplicacion autocontenida y genera el instalador y su checksum en `dist\`.

## Privacidad y limitaciones

La aplicacion no usa red, cuentas, telemetria ni bandeja del sistema. La opcion "siempre visible" se aplica al escritorio normal de Windows; las pantallas seguras del sistema y algunas aplicaciones a pantalla completa pueden mostrarse por encima.
