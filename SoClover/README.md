## Deployment process

### Step1: On local
1) From \so-clover directory, create .tar archive: ```tar -czf SoClover.tgz SoClover```
2) Push .tgz to server to SoClover directory: ```scp .\SoClover.tgz <user>@<server_ip>:/opt/soclover/```

If you updated .dockerignore -> you have to push it manually to /SoClover as it's not part of the .tar file. 
If you did not updated .dockerignore -> You can proceed to Step2

### Step2: On server
1) Check the .tgz has been uploaded: ```cd /opt/soclover ls -la SoClover.tgz```
1) Extract .tgz file: ```tar -xzf SoClover.tgz```
2) Delete .tgz file after extraction: ```rm -f SoClover.tgz```
   3) Optional: Double check folder exists and contains compose.yaml & Dockerfile:
   4) ```ls -la /opt/soclover/SoClover ls -la /opt/soclover/SoClover/compose.yaml /opt/soclover/SoClover/Dockerfile```
3) Empty server cache: ```docker builder prune -af -f```
4) Build new version: ```docker compose build --no-cache web```
5) Run new version: ```docker compose up -d```