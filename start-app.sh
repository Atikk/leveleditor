#!/bin/bash

# Kill any existing servers
killall Xvnc 2>/dev/null || true
killall python3 2>/dev/null || true

# Start Xvnc directly without vncserver wrapper
Xvnc :0 -geometry 1280x720 -depth 24 -rfbport 5900 -SecurityTypes None -AlwaysShared &
XVNC_PID=$!

# Wait for Xvnc to start
sleep 2

# Set DISPLAY
export DISPLAY=:0

# Start window manager
fluxbox &

# Wait a bit for WM to start
sleep 1

# Start the Avalonia demo application in background
cd /home/runner/workspace/avalonia-demo/DotGameAvalonia
dotnet run &
APP_PID=$!

# Start noVNC web interface on port 5000
echo "Starting noVNC on port 5000..."
/nix/store/n7h60i6lqysmya4clas5vghfsjc6sspa-novnc-1.6.0/bin/novnc --listen 5000 --vnc localhost:5900 &
NOVNC_PID=$!

echo "Xvnc started (PID: $XVNC_PID)"
echo "DotGame started (PID: $APP_PID)"
echo "noVNC web interface available at http://0.0.0.0:5000"

# Wait for noVNC process
wait $NOVNC_PID
