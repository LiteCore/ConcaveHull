using NetTopologySuite.Operation.Distance;
using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Algorithm;
using GeoAPI.Geometries;

namespace ConcaveHullProject
{
    class ConcaveHull
    {
        public static IPolygon GetConcaveHull(List<Coordinate> points, double concavity, double maxLenght)
        {
            //создаем convexhull
            var hull = (new ConvexHull(points.ToArray(), NetTopologySuite.Geometries.GeometryFactory.Default)).GetConvexHull();
            List<Coordinate> coords = hull.Coordinates.ToList();
            //удаляем первую точку для того что бы получить кольцо из точек
            coords.RemoveAt(0);
            foreach (var c in coords)
            {
                points.RemoveWhere(x => x.X == c.X && x.Y == c.Y);
            }
            var factory = NetTopologySuite.Geometries.Geometry.DefaultFactory;
            for (int i = 0; i < coords.Count; i++)
            {
                ClearLines(coords);
                var line = factory.CreateLineString(new Coordinate[] { GetElement(coords, i), GetElement(coords, i + 1) });
                if (line.Length <= maxLenght)
                {
                    continue;
                }
                //берем левого и правого соседа отрезка
                var leftLine = factory.CreateLineString(new Coordinate[] { GetElement(coords, i - 1), GetElement(coords, i) });
                var rightLine = factory.CreateLineString(new Coordinate[] { GetElement(coords, i + 1), GetElement(coords, i + 2) });
                //находим ближайшую точку от центра отрезка, но что бы соседнии линии не были к ней ближе
                var nearestPoint = GetNearestPoint(points, line, leftLine, rightLine);
                if (nearestPoint.Item1 == null)
                {
                    continue;
                }
                var eh = line.Length;
                var dd = nearestPoint.Item2;
                //находм соотножение отрезка к расстоянию до точки и проверяем не будут ли новые отрезки пересекать область
                if ((eh / dd) > concavity && !IsIntersectsLines(line, nearestPoint.Item1, coords))
                {
                    //добавляем новую точку между точками основного отрезка
                    coords.Insert((i + 1) % points.Count, nearestPoint.Item1);
                    //удаляем из точек ближайшую точку для избежания повторной привязки к ней
                    points.RemoveWhere(x => x.X == nearestPoint.Item1.X && x.Y == nearestPoint.Item1.Y);
                    i = -1;
                }
            }
            //замыкаем коллекцию точек для конвертации в полигон
            coords.Add(coords.First());
            return factory.CreatePolygon(coords.ToArray());
        }

        private static double GetMaxLenght(List<Coordinate> coords)
        {
            var factory = NetTopologySuite.Geometries.Geometry.DefaultFactory;
            var maxDist = double.NegativeInfinity;
            for (int i = 0; i < coords.Count; i++)
            {
                var line = factory.CreateLineString(new Coordinate[] { GetElement(coords, i), GetElement(coords, i + 1) });
                if(line.Length > maxDist)
                {
                    maxDist = line.Length;
                }
            }
            return maxDist / 8;
        }

        private static bool IsIntersectsLines(ILineString segment, Coordinate point, List<Coordinate> coords)
        {
            var factory = NetTopologySuite.Geometries.Geometry.DefaultFactory;
            var firstLine = factory.CreateLineString(new Coordinate[] { segment.StartPoint.Coordinate, point });
            var secondLine = factory.CreateLineString(new Coordinate[] { segment.EndPoint.Coordinate, point });
            for (int i = 0; i < coords.Count; i++)
            {
                var line = factory.CreateLineString(new Coordinate[] { GetElement(coords, i), GetElement(coords, i + 1) });
                if (firstLine.Crosses(line) || secondLine.Crosses(line))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ClearLines(List<Coordinate> coordinates)
        {
            coordinates = coordinates.Distinct(new CoordinateComparer()).ToList();
        }
        
        private static (Coordinate, double) GetNearestPoint(List<Coordinate> coordinates, ILineString line, ILineString left, ILineString right)
        {
            var factory = NetTopologySuite.Geometries.Geometry.DefaultFactory;
            var dist = double.PositiveInfinity;
            Coordinate res = null;
            var middlePoint = GetMiddlePoint(line);
            foreach (var point in coordinates)
            {
                var p = factory.CreatePoint(point);
                var d = GetDistance(middlePoint, p);
                var l = GetDistance(left, p);
                var r = GetDistance(right, p);
                if (d < dist && d != 0d && l > d && r > d)
                {
                    dist = d;
                    res = point;
                }
            }
            return (res, dist);
        }

        private static IPoint GetMiddlePoint(ILineString line)
        {
            return NetTopologySuite.Geometries.Geometry.DefaultFactory.CreatePoint(new Coordinate((line.StartPoint.X + line.EndPoint.X) / 2, (line.StartPoint.Y + line.EndPoint.Y) / 2));
        }

        private static Coordinate GetElement(List<Coordinate> points, int i)
        {
            if (i >= points.Count)
            {
                return points[i % points.Count];
            }
            else if (i < 0)
            {
                return points[(i % points.Count) + points.Count];
            }
            else
            {
                return points[i];
            }
        }
        private static double GetDistance(ILineString line, IPoint point)
        {
            var x0 = point.X;
            var y0 = point.Y;
            var x1 = line.StartPoint.X;
            var x2 = line.EndPoint.X;
            var y1 = line.StartPoint.Y;
            var y2 = line.EndPoint.Y;
            return DistanceComputer.PointToSegment(point.Coordinate, line.StartPoint.Coordinate, line.EndPoint.Coordinate);
            //return Math.Abs((((y2 - y1) * point.X) - ((x2 - x1) * point.Y) + (x2 * y1) - (y2 * x1))) / (Math.Sqrt(Math.Pow(y2 - y1, 2) + Math.Pow(x2 - x1, 2)));

        }
        private static double GetDistance(IPoint point1, IPoint point2)
        {
            return DistanceOp.Distance(point1, point2);
        }
    }
}
