#!/bin/bash

# Wait for VNC server to be ready
sleep 2

# Set DISPLAY environment variable
export DISPLAY=:0

# Run the DotGame application
cd src/DotGameCSharp
dotnet run
