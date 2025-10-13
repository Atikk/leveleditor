#!/bin/bash

# Set up VNC password
mkdir -p ~/.vnc
echo "replit" | vncpasswd -f > ~/.vnc/passwd
chmod 600 ~/.vnc/passwd

# Create xstartup script
cat > ~/.vnc/xstartup << 'EOF'
#!/bin/bash
unset SESSION_MANAGER
unset DBUS_SESSION_BUS_ADDRESS
fluxbox &
xterm &
EOF
chmod +x ~/.vnc/xstartup

# Start VNC server on port 5900 (VNC display :0)
vncserver :0 -geometry 1280x720 -depth 24 -localhost no

# Keep the script running
tail -f ~/.vnc/*.log
