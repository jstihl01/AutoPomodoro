# Pomodoro para Windows 11

Temporizador Pomodoro de escritorio sincronizado con la hora real. Alterna automaticamente entre 25 minutos de trabajo y 5 minutos de descanso en cada media hora.

## Funcionamiento

- Trabajo: de `:00` a `:25` y de `:30` a `:55`.
- Descanso: de `:25` a `:30` y de `:55` a `:00`.
- Fondo verde durante el trabajo y azul durante el descanso.
- Siempre visible sobre las ventanas normales, redimensionable y sin controles aparte de cerrar.
- Un tono suave al comenzar el descanso y tres tonos al volver al trabajo.
- Una sola instancia y ninguna ejecucion en segundo plano al cerrar.
- Recuerda el tamano y la posicion en `%LocalAppData%\Pomodoro\settings.json`.

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
