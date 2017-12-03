# B315 auto reboot when connection is lost

This simple dotnet core 2.0 applicaiton tries to recover from weird bugs or connection problems experienced 
on Saunalahti Huawei B315s. It automatically reboots the AP when the connection is unavailable.
The idea is to have a linux/windows box with a scheduled job that executes this application periodically.
Code ported from https://github.com/kotylo/b315s-change-network
Jurassic library built from https://github.com/MaitreDede/jurassic/commits/dot-net-core commit 746fe6b83c36d186ed8130694112f372d366abc4

Note:
* .NET CORE doesn't run on raspberry PI 1 or Zero
* Code tested on raspberry pi 2 raspbian lite
* To install .net core on raspberry pi follow this tutorial https://jeremylindsayni.wordpress.com/2017/07/23/running-a-net-core-2-app-on-raspbian-jessie-and-deploying-to-the-pi-with-cake/
* To enable SSH on the raspberry pi look here https://www.raspberrypi.org/documentation/remote-access/ssh/ 
* To set a static IP look here https://www.modmypi.com/blog/tutorial-how-to-give-your-raspberry-pi-a-static-ip-address

# Build and deploy

In the development environment run the following commands:

1) dotnet resore

2) dotnet build 

3) dotnet publish -r linux-arm

4) Copy the the content of bin\Debug\netcoreapp2.0\linux-arm\publish to the raspberry pi /home/pi/fixapcore/FixApCore e.g. using WinSCP.

# Cron job setup

On the raspberry pi

1) Create a new bash file fixap.sh like the one below but replace [AP_IP] with your AP address (mine was 192.168.100.1), [AP_USER] with the AP user (mine was admin)
and [AP_PASSWORD] (the default is written at the bottom of the AP)

```{r, engine='bash', fixap}
#!/bin/bash
/home/pi/fixapcore/FixApCore http://[AP_IP] [AP_USER] [AP_PASSWORD]
```

2) crontab -e 

3) Add a new entry that runs the app every minute: 

```{r, engine='bash', cron}
* * * * * /home/pi/fixap.sh >> /home/pi/fixap.log 2>&1
```

4) Check the content of the log file to verify that everything run correctly
