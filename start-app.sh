#!/bin/bash

# Kill any existing VNC servers
vncserver -kill :0 2>/dev/null || true
vncserver -kill :1 2>/dev/null || true

# Set up VNC password
mkdir -p ~/.vnc
echo "replit" | vncpasswd -f > ~/.vnc/passwd
chmod 600 ~/.vnc/passwd

# Create xstartup script for VNC
cat > ~/.vnc/xstartup << 'XEOF'
#!/bin/bash
unset SESSION_MANAGER
unset DBUS_SESSION_BUS_ADDRESS
export DISPLAY=:0
fluxbox &
xterm -e "echo 'VNC Desktop Ready. DotGame will start automatically.'; sleep 2" &
sleep 2
cd /home/runner/workspace/src/DotGameCSharp
dotnet run
XEOF
chmod +x ~/.vnc/xstartup

# Start VNC server
vncserver :0 -geometry 1280x720 -depth 24 -localhost no

# Start noVNC web interface on port 5000
echo "Starting noVNC on port 5000..."
/nix/store/n7h60i6lqysmya4clas5vghfsjc6sspa-novnc-1.6.0/bin/novnc --listen 5000 --vnc localhost:5900 &

# Wait for services to start
sleep 3

echo "VNC Server started on display :0 (port 5900)"
echo "noVNC web interface available at http://0.0.0.0:5000"
echo "VNC password: replit"

# Keep the script running and show VNC logs
tail -f ~/.vnc/*.log
