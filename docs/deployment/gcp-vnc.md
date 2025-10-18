# Hosting the Level Editor on Google Cloud with Web VNC

This guide explains how to run the Avalonia editor inside a container that exposes a browser-based VNC session. The setup uses noVNC/websockify for the WebSocket bridge and is intended for deployment on Google Cloud Run (fully managed). The same container can also run on other services such as Compute Engine or GKE.

## Overview

- **Container image**: `cloud/gcp-vnc/Dockerfile` builds the editor together with an Xvfb virtual display, Fluxbox window manager, x11vnc server, and noVNC gateway.
- **Process control**: `supervisord` keeps Xvfb, Fluxbox, x11vnc, websockify, and the .NET application alive.
- **Ports**: The browser endpoint listens on port `6080` (Cloud Run automatically maps the provided `$PORT`).
- **Authentication**: By default x11vnc is configured without a password. Add Cloud Run authentication or x11vnc auth for production.

## Prerequisites

1. Google Cloud CLI (`gcloud`) authenticated for your project.
2. Artifact Registry repository for container images (e.g. `projects/PROJECT_ID/locations/us-central1/repositories/leveleditor`).
3. Cloud Run API enabled.

## Build & Push with Cloud Build

1. Update substitutions in `cloud/gcp-vnc/cloudbuild.yaml` if you prefer a different region, repository, or tag.
2. Submit a Cloud Build:

   ```bash
   gcloud builds submit \
     --config cloud/gcp-vnc/cloudbuild.yaml \
     --project PROJECT_ID \
     --substitutions _REGION=us-central1,_REPOSITORY=leveleditor,_TAG=latest
   ```

   The build stage compiles the Avalonia project (`dotnet publish`) and produces a container image tagged as:

   ```
   us-central1-docker.pkg.dev/PROJECT_ID/leveleditor/leveleditor-vnc:latest
   ```

## Deploy to Cloud Run

1. Deploy the image (adjust CPU/memory if needed):

   ```bash
   gcloud run deploy leveleditor-vnc \
     --image us-central1-docker.pkg.dev/PROJECT_ID/leveleditor/leveleditor-vnc:latest \
     --platform managed \
     --region us-central1 \
     --allow-unauthenticated \
     --port 6080 \
     --cpu 2 \
     --memory 2Gi \
     --timeout 900s
   ```

2. After deployment finishes, note the service URL (e.g. `https://leveleditor-vnc-xxxx-uc.a.run.app`).

3. Connect with a browser using the bundled noVNC client:

   ```
   https://SERVICE_URL/vnc.html?resize=remote&autoconnect=1
   ```

   The editor window appears after Fluxbox launches and the .NET process starts (first load may take ~10 seconds).

## Security Hardening

- **Authentication**: Enable Cloud Run IAM authentication or add `-rfbauth` with a password file to the x11vnc command in `supervisord.conf`.
- **TLS**: Cloud Run terminates HTTPS automatically. For other compute targets, use HTTPS via an external load balancer or reverse proxy.
- **Session timeouts**: Consider adding supervising logic to recycle idle sessions.

## Customising the Image

- **Resolution**: Update the Xvfb screen size in `supervisord.conf` (`1280x720x24`). Larger resolutions require more memory.
- **Window manager**: Fluxbox is lightweight; replace it with another X11 WM if you need more features.
- **App launch command**: Modify the `command` under `[program:app]` in `supervisord.conf` to run alternative binaries or pass arguments (e.g. load specific project files).

## Deploying on Compute Engine or GKE

The same container can run on other platforms:

- **Compute Engine**: Create a VM with Container-Optimized OS or Ubuntu, install Docker, and `docker run -p 80:6080 us-central1-docker.pkg.dev/.../leveleditor-vnc:latest`.
- **GKE Autopilot/Standard**: Create a Deployment exposing `containerPort: 6080` and front it with an HTTPS load balancer (Ingress).

## Troubleshooting

| Symptom | Likely Cause | Fix |
| --- | --- | --- |
| Blank browser screen | Avalonia app still starting or crashed | Check Cloud Run logs (`gcloud logs read --service leveleditor-vnc`) |
| "Server disconnected" in noVNC | x11vnc not reachable | Ensure port mapping and supervisor processes are healthy |
| High CPU usage | Xvfb / rendering load | Increase CPU allocation or lower resolution |

## File Reference

- `cloud/gcp-vnc/Dockerfile` – multi-stage build container.
- `cloud/gcp-vnc/startup.sh` – sets runtime port and launches supervisord.
- `cloud/gcp-vnc/supervisord.conf` – orchestrates Xvfb, Fluxbox, x11vnc, websockify, and the editor.
- `cloud/gcp-vnc/cloudbuild.yaml` – Cloud Build pipeline to build & push the image.
