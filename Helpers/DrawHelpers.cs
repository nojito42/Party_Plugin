using ExileCore;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Party_Plugin.Helpers;
public static class DrawHelpers 
{

    public static void DrawEllipseToWorld(this PartyPlugin p, System.Numerics.Vector3 pos, int radius, int points, int lineWidth, Color color)
    {
        var plottedCirclePoints = new List<System.Numerics.Vector3>();
        var slice = 2 * Math.PI / points;
        for (var i = 0; i < points; i++)
        {
            var angle = slice * i;
            var x = (decimal)pos.X + decimal.Multiply(radius, (decimal)Math.Cos(angle));
            var y = (decimal)pos.Y + decimal.Multiply(radius, (decimal)Math.Sin(angle));
            plottedCirclePoints.Add(new System.Numerics.Vector3((float)x, (float)y, pos.Z));
        }

        for (var i = 0; i < plottedCirclePoints.Count; i++)
        {
            if (i >= plottedCirclePoints.Count - 1)
            {
                var pointEnd1 = p.Cam.WorldToScreen(plottedCirclePoints.Last());
                var pointEnd2 = p.Cam.WorldToScreen(plottedCirclePoints[0]);
                p.Graphics.DrawLine(pointEnd1, pointEnd2, lineWidth, color);
                return;
            }

            var point1 = p.Cam.WorldToScreen(plottedCirclePoints[i]);
            var point2 =    p.Cam.WorldToScreen(plottedCirclePoints[i + 1]);
            p.Graphics.DrawLine(point1, point2, lineWidth, color);
        }
    }

}

