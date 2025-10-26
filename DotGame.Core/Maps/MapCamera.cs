using System;

namespace DotGame.Core;

public class MapCamera
{
    // Optional 3D position [x, y, z]
    public double[]? Position { get; set; }

    // Optional quaternion rotation [x, y, z, w]
    public double[]? RotationQuaternion { get; set; }
}
