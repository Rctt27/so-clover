## Deployment process

### Step1: On local
1) From \so-clover directory, create .tgz archive: ```tar -czf SoClover.tgz SoClover```
2) Push .tgz to server to SoClover directory: ```scp .\SoClover.tgz <user>@<server_ip>:/opt/soclover/```

If you updated .dockerignore -> you have to push it manually to /opt/soclover root directory as it's not part of the .tar file. 
If you did not updated .dockerignore -> You can proceed to Step2

### Step2: On server
1) Check the .tgz has been uploaded: 
   2) ```cd /opt/soclover```
   3) ```ls -la SoClover.tgz```
1) Extract .tgz file: ```tar -xzf SoClover.tgz```
2) Delete .tgz file after extraction: ```rm -f SoClover.tgz```
   3) Optional: Double check folder exists and contains compose.yaml & Dockerfile:
   4) ```ls -la /opt/soclover/SoClover```
   5) ```ls -la /opt/soclover/SoClover/compose.yaml /opt/soclover/SoClover/Dockerfile```
3) Empty server cache: ```docker builder prune -af -f```
4) Build new version:
   5) ```cd /opt/soclover/SoClover```
   6) ```docker compose build --no-cache web```
5) Run new version: ```docker compose up -d```


### Misc: Server checks and actions

1) Check partition load: ```df -h``` / ```sudo ncdu / --exclude /opt/soclover```
2) Delete unused Docker containers: ```docker system prune -a --volumes```
3) List Docker images: ```docker images```
4) Empty Docker logs: ```sudo journalctl --vacuum-size=100M```
5) Clean temporary files:
   6) ```sudo apt clean```
   7) ```sudo rm -rf /tmp/*```
   8) ```sudo rm -rf /var/tmp/*```
9) Reboot Docker: ```sudo systemctl restart docker```
10) Stop all running Docker containers: ```docker stop $(docker ps -aq)```
11) Delete unused network : ```docker network prune -f```
12) Delete unused images : ```docker image prune -a -f```







