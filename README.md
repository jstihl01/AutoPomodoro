# AutoPomodoro para Windows 11

[![Descargar AutoPomodoro para Windows](https://img.shields.io/badge/DESCARGAR-AutoPomodoro%20para%20Windows-1673C9?style=for-the-badge)](https://github.com/jstihl01/AutoPomodoro/releases/download/v2.0.1/AutoPomodoro-Setup-v2.0.1.exe)

Temporizador Pomodoro de escritorio sincronizado con la hora local de Windows. Abre la aplicación y el contador se ajusta a la fase que corresponda.

## Instalar

El botón descarga la versión estable 2.0.1 para Windows 11 x64. El instalador funciona por usuario y crea accesos en el escritorio y en el menú Inicio. Volver a ejecutarlo actualiza la aplicación y conserva los ajustes.

Windows SmartScreen puede advertir que el instalador 2.0.1 carece de firma Authenticode. Para comprobar la integridad de la descarga, compara el resultado con el [SHA-256 de v2.0.1](https://github.com/jstihl01/AutoPomodoro/releases/download/v2.0.1/AutoPomodoro-Setup-v2.0.1.exe.sha256). El SHA-256 verifica el archivo, mientras que la firma Authenticode identifica al editor.

```powershell
Get-FileHash .\AutoPomodoro-Setup-v2.0.1.exe -Algorithm SHA256
```

Desde 2.0.2, la desinstalación retira solo los archivos de ajustes propios de `%LocalAppData%\AutoPomodoro` y elimina la carpeta si queda vacía. La carpeta heredada `%LocalAppData%\Pomodoro` se conserva íntegra. La versión 2.0.1 elimina ambas carpetas completas al desinstalar.

## Funcionamiento

- **25/5**. Trabajo de `:00` a `:25` y de `:30` a `:55`, con fondo verde. Los descansos son azules.
- **50/10**. Trabajo de `:00` a `:50`, con fondo rojo. Los últimos diez minutos de la hora son de descanso.
- Un tono suave al comenzar el descanso y tres tonos al volver al trabajo. Se recalcula la fase en silencio al abrir la app, cambiar de modo o cambiar la hora del sistema.
- La ventana es redimensionable y permanece encima de las ventanas normales. Ciérrala con `Alt+F4` o desde la barra de tareas.
- En Ajustes se eligen el modo y el volumen. El altavoz reproduce una prueba con el volumen seleccionado. Al pulsar **LISTO** o `Esc`, se aplican y se guardan los cambios. **Restablecer ajustes** devuelve también el tamaño y la posición originales.
- La app solicita a Windows que mantenga el equipo y la pantalla activos mientras está abierta. Los problemas de audio, energía y guardado se indican en la vista activa.

### Widget en la barra de tareas

- **Mostrar en barra de tareas** sustituye la ventana por una cápsula junto al área de notificación. La cápsula indica la fase por escrito y muestra `25/5` o `50/10` durante el trabajo.
- Pasa el puntero por el altavoz para abrir el volumen. Púlsalo para silenciar o restaurar el último valor audible. Con el teclado, activa **Mostrar en barra de tareas** desde la ventana y pulsa `F2` en el widget para enfocar el deslizador. `Esc` devuelve el foco al altavoz.
- Pulsa el contador para recuperar la ventana principal. Allí está el engranaje de **Ajustes**, en la esquina superior derecha. También puedes recuperar la ventana al abrir el acceso directo de nuevo.
- `Alt+F4` desde el widget cierra AutoPomodoro. El contador y los avisos funcionan en ambas vistas.
- El widget es una ventana independiente que se coloca sobre la barra horizontal principal. Comparte espacio con sus iconos y se oculta con la barra o ante una ventana a pantalla completa. Si la barra deja de estar disponible, se recupera la ventana principal y se conserva la vista preferida para el próximo arranque.

Los ajustes se guardan en `%LocalAppData%\AutoPomodoro\settings.json`. Si hay una lectura fallida, la app conserva el archivo y busca un respaldo o los ajustes heredados. Tras un fallo de guardado puedes reintentar al pulsar **LISTO** en Ajustes. AutoPomodoro avisa antes de salir si quedan cambios pendientes.

## Compilar

- Windows 11 x64
- SDK de .NET 10 indicado en `global.json`
- Inno Setup 6.3.0 o posterior
- Git

```powershell
.\build.ps1 -TestInstaller
```

El script comprueba el compilador de Inno Setup antes de compilar, ejecuta las pruebas de reloj e integración WPF y comprueba el arranque y el runtime del ejecutable autocontenido. `-TestInstaller` añade la instalación, reinstalación y desinstalación en rutas temporales con un identificador distinto. Los artefactos quedan en `releases\` con el número de versión en el nombre. Conserva juntos el instalador, su SHA-256 y el archivo `.build.json`, que registra el commit, el estado de las fuentes, el SDK, Inno Setup y los runtimes incluidos.

Para una entrega firmada, prepara un commit limpio, un certificado de firma de código con clave privada en `CurrentUser\My` y el `signtool.exe` del SDK de Windows.

```powershell
.\build.ps1 -TestInstaller -Release -SignTool 'C:\ruta\signtool.exe' -CertificateThumbprint '<huella>'
```

Ese modo verifica la firma y el sello de tiempo del instalador y del desinstalador antes de generar el checksum. Vuelve a compilar y distribuir la aplicación al incorporar parches del runtime autocontenido. Actualiza el instalador y el checksum del README a la misma etiqueta de GitHub y publica el manifiesto de la misma compilación.

## Privacidad y limitaciones

La aplicación funciona sin red, cuentas ni telemetría. Windows conserva el control sobre los bloqueos manuales, las políticas de energía, los protectores de pantalla y la suspensión forzada. La superposición se aplica al escritorio normal.
