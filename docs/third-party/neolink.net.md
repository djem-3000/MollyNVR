# neolink.net (AGPL-3.0)

A bridge for Reolink cameras that do not provide RTSP/ONVIF and are reachable only over the proprietary
**Baichuan** protocol (TCP 9000). It runs as a separate `reolink` service in `compose.yaml`.

- Project: <https://github.com/borexola/neolink.net>
- License: **GNU Affero General Public License v3.0** — <https://www.gnu.org/licenses/agpl-3.0.html>
- Version in use: `ghcr.io/borexola/neolink.net:1.2.2`
- Our image: `docker/reolink/Dockerfile` (the same binary plus the `entrypoint.sh` supervisor).

## How it relates to MollyNVR

The MollyNVR code is **not linked** with neolink.net and does not include its sources: it is a separate
process, and the interaction goes over the HTTP API and RTSP. Therefore the AGPL applies to neolink.net
itself, not to the application code.

The AGPL requires providing the source code to users who interact with the program over a network.
The sources are available at the link above; we do not modify them — we only run them in a container.

## What MollyNVR writes to the bridge configuration

The `data/reolink/config.json` file (inside the container — `/config/config.json`):

- the camera name (`cam-<ip>`), address, login and **password** of the camera (the bridge needs to sign in);
- other configuration fields are left untouched.

The password is stored in plain text — this is a neolink.net requirement (it reads only the file).
The `data/` directory is excluded from git; access to the file is limited by file system permissions.

## Updating

The version is pinned in `docker/reolink/Dockerfile` (the image tag). Before updating, review
the changelog and the license of the project.
