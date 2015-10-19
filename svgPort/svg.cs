﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Autodesk.DesignScript.Geometry;
using System.IO;

namespace Illustrator
{
    public class SVG
    {
        private SVG()
        {
        }

        private static StreamWriter CreateNewSVGFile(String filePath, String fileName)
        {
            //check for invalid characters in the file name
            char[] invalidFileChars = Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(invalidFileChars) != -1 || fileName.CompareTo("CON") == 0)
                throw new ArgumentException("The file name does not satisfy valid windows file name criteria", "fileName");

            StreamWriter file = new StreamWriter(Path.Combine(filePath, fileName + ".svg"));
            return file;
        }

        private static void preSVGBody(StreamWriter file)
        {
            String line1 = @"<?xml version=""1.0"" encoding=""iso-8859-1""?>";
            String line2 = "<!-- Generator: Dynamo SVG Export Addon. visit www.dynamobim.org  -->";
            String line3 = "<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">";

            file.WriteLine(line1);
            file.WriteLine(line2);
            file.WriteLine(line3);
        }

        #region Write methods

        private static void WriteLine(StreamWriter file, Line line)
        {
            Point startPt = line.StartPoint;
            Point endPt = line.EndPoint;

            double x1 = startPt.X;
            double y1 = startPt.Y;

            double x2 = endPt.X;
            double y2 = endPt.Y;

            file.WriteLine("<line x1='" + x1 + "' y1='" + y1 + "' x2='" + x2 + "' y2='" + y2 + "' style='stroke:black; stroke-width: 1;'/>");

        }

        private static void WriteCircle(StreamWriter file, Circle circle)
        {
            double x = circle.CenterPoint.X;
            double y = circle.CenterPoint.Y;

            file.WriteLine("<circle cx='" + x + "' cy='" + y + "' r='" + circle.Radius + "' fill='none' stroke='red' stroke-width='1'/>");
        }

        private static void WriteEllipse(StreamWriter file, Ellipse ellipse)
        {
            Point centerPt = ellipse.CenterPoint;
            var majorAxis = ellipse.MajorAxis;
            var minorAxis = ellipse.MinorAxis;

            file.WriteLine("<ellipse cx='" + centerPt.X + "' cy='" + centerPt.Y + 
                "' rx='" + majorAxis.Length + "' ry='" + minorAxis.Length + 
                "' transform='rotate(" + Math.Atan(majorAxis.Y / majorAxis.X) * 180 / Math.PI + ", " + centerPt.X + ", " + centerPt.Y + ")' style='stroke:black; stroke-width: 1;'/>");
        }

        private static void WritePolygon(StreamWriter file, Polygon polygon)
        {
            var vertices = new List<Point>();
            vertices.AddRange(polygon.Points);

            String collectedString = "";
            foreach (var vertex in vertices)
            {
                collectedString = collectedString + vertex.X + ", " + vertex.Y + " ";
            }

            file.WriteLine("<polygon points='" + collectedString + "' style='fill: none; stroke-width: 1; stroke: #000000;'/>");
        }

        private static void WriteNurbsCurve(StreamWriter file, NurbsCurve nurbsCurve)
        {
            var pathString = "<path d='";

            Point[][] bCurve = DecomposeNurbsCurve(nurbsCurve);

            pathString += "M" + bCurve[0][0].X + "," + bCurve[0][0].Y + " C";

            for (int m = 0; m < bCurve.Count(); ++m)
            {
                for (int n = 1; n < bCurve[m].Count(); ++n)
                {
                    pathString += bCurve[m][n].X + "," + bCurve[m][n].Y + " ";

                }

            }
            pathString += "' fill='none' stroke='red' stroke-width='1' />";
            file.WriteLine(pathString);

        }

        #endregion

        private static void ComputeBoundingBox(Geometry[] geometry, out Point minPt, out Point maxPt)
        {
            const double MAXNUM = 1000000;
            const double MINNUM = -1000000;
            double minX = MAXNUM, minY = MAXNUM, minZ = MAXNUM;
            double maxX = MINNUM, maxY = MINNUM, maxZ = MINNUM;
            foreach (var geom in geometry)
            {
                var boundingBox = BoundingBox.ByGeometry(geom);
                var maxBBPt = boundingBox.MaxPoint;
                var minBBPt = boundingBox.MinPoint;

                if (minX >= minBBPt.X)
                    minX = minBBPt.X;

                if (minY >= minBBPt.Y)
                    minY = minBBPt.Y;

                if (minZ >= minBBPt.Z)
                    minZ = minBBPt.Z;

                if (maxX <= maxBBPt.X)
                    maxX = maxBBPt.X;

                if (maxY <= maxBBPt.Y)
                    maxY = maxBBPt.Y;

                if (maxZ <= maxBBPt.Z)
                    maxZ = maxBBPt.Z;
            }
            minPt = Point.ByCoordinates(minX, minY, minZ);
            maxPt = Point.ByCoordinates(maxX, maxY, maxZ);
        }


        /// <summary>
        /// Exports Dynamo geometry as an SVG File.
        /// Supported geometry includes Point, Line, Circle, Ellipse, Polygon and NurbsCurve.
        /// PolyCurve is not supported currently. The default viewport width and height
        /// are equal to the bounds of the geometry along the X and Y axes respectively.
        /// Only NurbsCurve of degree 3 can be converted.
        /// </summary>
        /// <param name="geometry">1D list of Geometry</param>
        /// <param name="exportLocation">Folder location for saving SVG file</param>
        /// <param name="fileName"></param>
        /// <param name="viewportWidth">
        /// If no viewport width is specified, the default width is taken to be the bounds
        /// of the input geometry along the X-axis
        /// </param>
        /// <param name="viewportHeight"></param>
        /// If no viewport height is specified, the default height is taken to be the bounds
        /// of the input geometry along the Y-axis
        /// <param name="units">
        /// Accepted units are "em", "ex", "px", "pt", "pc", "cm", "mm", "in".
        /// The default is pixels ("px").
        /// </param>
        /// <search>
        /// export, SVG, Illustrator
        /// </search>
        public static void Export(Geometry[] geometry, String exportLocation, string fileName, 
            double viewportWidth = 0, double viewportHeight = 0, string units = "px")
        {
            Point minPt, maxPt;
            ComputeBoundingBox(geometry, out minPt, out maxPt);

            //TODO: Handle replication for geometry

            var file = CreateNewSVGFile(exportLocation, fileName);

            //fill the SVG headers
            preSVGBody(file);

            if (viewportWidth == 0)
            {
                viewportWidth = (maxPt.X - minPt.X);
            }
            if (viewportHeight == 0)
            {
                viewportHeight = (maxPt.Y - minPt.Y);
            }
            //start the svg tag
            file.WriteLine("<svg version='1.1' xmlns='http://www.w3.org/2000/svg' " +
                           "xmlns:xlink='http://www.w3.org/1999/xlink' " +
                           "width='" + viewportWidth + units + "' height='" + viewportHeight +
                           units + "' viewBox='0 0 " + (maxPt.X - minPt.X) + " " + (maxPt.Y - minPt.Y) + "' xml:space='preserve'> ");


            //segregate all points
            List<Point> pts = new List<Point>();
            List<Line> lines = new List<Line>();
            List<Ellipse> ellipses = new List<Ellipse>();
            List<Circle> circles = new List<Circle>();
            List<Polygon> polygons = new List<Polygon>();

            List<NurbsCurve> nurbsCurves = new List<NurbsCurve>();
            var polyCurves = new List<PolyCurve>();
            //TODO: Need to support paths


            for (int i = 0; i < geometry.Length; ++i)
            {
                geometry[i] = geometry[i].Translate(0 - minPt.X, 0 - minPt.Y, 0);

                var geomType = geometry[i].GetType();
                if (geomType == typeof(Point))
                    pts.Add((Point)geometry[i]);

                else if (geomType == typeof(Line))
                    lines.Add((Line)geometry[i]);

                else if (geomType == typeof(Ellipse))
                    ellipses.Add((Ellipse)geometry[i]);

                else if (geomType == typeof(Circle))
                    circles.Add((Circle)geometry[i]);

                else if (geomType == typeof(Polygon))
                    polygons.Add((Polygon)geometry[i]);

                else if (geomType == typeof(NurbsCurve))
                    nurbsCurves.Add((NurbsCurve)geometry[i]);

                else if (geomType == typeof(PolyCurve))
                    polyCurves.Add((PolyCurve)geometry[i]);

            }

            //TODO: currently Z values are ignored, need a better way to do this
            //TODO: segregate points by layers



            //write all points into the file
            for (int i = 0; i < pts.Count; ++i)
            {
                if (i == 0)
                    file.WriteLine("<g>");

                double x = pts[i].X;
                double y = pts[i].Y;

                file.WriteLine("<circle cx='" + x + "' cy='" + y + "' r='1' fill='black'/>");

                if (i == pts.Count - 1)
                    file.WriteLine("</g>");
            }


            //write all lines into the file
            for (int i = 0; i < lines.Count; ++i)
            {
                if (i == 0)
                    file.WriteLine("<g>");

                WriteLine(file, lines[i]);

                if (i == lines.Count - 1)
                    file.WriteLine("</g>");
            }


            //write all ellipses into the file
            for (int i = 0; i < ellipses.Count; ++i)
            {
                if (i == 0)
                    file.WriteLine("<g>");

                WriteEllipse(file, ellipses[i]);

                if (i == ellipses.Count - 1)
                    file.WriteLine("</g>");
            }

            //write all circles into the file
            for (int i = 0; i < circles.Count; ++i)
            {
                if (i == 0)
                    file.WriteLine("<g>");

                WriteCircle(file, circles[i]);

                if (i == circles.Count - 1)
                    file.WriteLine("</g>");
            }

            //write all polygons into the file
            for (int i = 0; i < polygons.Count; ++i)
            {
                if (i == 0)
                    file.WriteLine("<g>");

                WritePolygon(file, polygons[i]);

                if (i == polygons.Count - 1)
                    file.WriteLine("</g>");
            }

            //write all nurbscurve into a file
            for (int i = 0; i < nurbsCurves.Count; ++i)
            {
                if (i == 0)
                    file.WriteLine("<g>");

                WriteNurbsCurve(file, nurbsCurves[i]);

                if (i == nurbsCurves.Count - 1)
                    file.WriteLine("</g>");
            }

            foreach (var polyCurve in polyCurves)
            {
                file.WriteLine("<g>");
                foreach (var curve in polyCurve.Curves())
                {
                    if (curve is NurbsCurve)
                    {
                        WriteNurbsCurve(file, ((NurbsCurve)curve));
                    }
                    else if (curve is Line)
                    {
                        WriteLine(file, ((Line)curve));
                    }
                    else if (curve is Circle)
                    {
                        WriteCircle(file, ((Circle)curve));
                    }
                    else if (curve is Ellipse)
                    {
                        WriteEllipse(file, ((Ellipse)curve));
                    }
                    else if (curve is Polygon)
                    {
                        WritePolygon(file, ((Polygon)curve));
                    }
                }
                file.WriteLine("</g>");
            }
            //complete the svg tag
            file.WriteLine("</svg>");
            file.Close();
        }

        #region NURBS to Bezier Helpers

        private static Point[][] DecomposeNurbsCurve(NurbsCurve nurbCurve)
        {
            if(nurbCurve.Degree != 3)
                throw  new Exception("Only degree 3 NURBS curves can be exported to SVG");
            var P = nurbCurve.ControlPoints();
            int p = nurbCurve.Degree;
            var U = nurbCurve.Knots();
            int m = P.Length + p;
            Debug.Assert(U.Length == m + 1);

            // Assuming this is a clamped NURBS curve with knot vector
            // {U0, ..., Up, Up+1, ..., Um-p-1, Um-p ... Um}
            // we insert each of the internal knots {Up+1, ..., Um-p-1}
            // p-1 times. 
            int n = P.Length - 1;
            int nb;
            Point[][] Q;
            DecomposeCurve(n, p, U, P, out nb, out Q);
            return Q;
        }

        struct point
        {
            public double X;
            public double Y;
            public double Z;
        }

        private static void DecomposeCurve(int n, int p, double[] U,
            Point[] P, out int nb, out Point[][] Q)
        {
            int m = n + p + 1;
            int a = p;
            int b = p + 1;
            nb = 0;
            // If there are m+1 knots and a clamped knot vector
            // The number of knot intervals would ideally be (m-2p)
            // which is also the max number of Bezier curves that can be created
            point[,] qq = new point[m - 2 * p, p + 1];
            for (int i = 0; i <= p; i++)
            {
                qq[nb, i].X = P[i].X;
                qq[nb, i].Y = P[i].Y;
                qq[nb, i].Z = P[i].Z;
            }
            while (b < m)
            {
                int i = b;
                while (b < m && U[b + 1] == U[b])
                    b++;
                int mult = b - i + 1;
                if (mult < p)
                {
                    double[] alphas = new double[p];
                    var numer = U[b] - U[a];
                    for (int j = p; j > mult; j--)
                    {
                        alphas[j - mult - 1] = numer / (U[a + j] - U[a]);
                    }
                    int r = p - mult;
                    for (int j = 1; j <= r; j++)
                    {
                        var save = r - j;
                        int s = mult + j;
                        for (int k = p; k >= s; k--)
                        {
                            var alpha = alphas[k - s];
                            qq[nb, k].X = alpha * qq[nb, k].X + (1 - alpha) * qq[nb, k - 1].X;
                            qq[nb, k].Y = alpha * qq[nb, k].Y + (1 - alpha) * qq[nb, k - 1].Y;
                            qq[nb, k].Z = alpha * qq[nb, k].Z + (1 - alpha) * qq[nb, k - 1].Z;
                        }
                        if (b < m)
                        {
                            qq[nb + 1, save] = qq[nb, p];
                        }
                    }
                }
                nb = nb + 1;
                if (b < m)
                {
                    for (int j = p - mult; j <= p; j++)
                    {
                        qq[nb, j].X = P[b - p + j].X;
                        qq[nb, j].Y = P[b - p + j].Y;
                        qq[nb, j].Z = P[b - p + j].Z;
                    }
                    a = b;
                    b = b + 1;
                }
            }
            int nrows = qq.GetLength(0);
            Q = new Point[nrows][];
            for (int i = 0; i < nrows; i++)
            {
                Q[i] = new Point[p + 1];
                for (int j = 0; j <= p; j++)
                {
                    Q[i][j] = Point.ByCoordinates(
                        qq[i, j].X, qq[i, j].Y, qq[i, j].Z);
                }
            }
        }

        #endregion
    }
}
