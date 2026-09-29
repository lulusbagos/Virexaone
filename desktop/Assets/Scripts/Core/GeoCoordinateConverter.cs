using System;
using UnityEngine;

namespace Virexa.FMS
{
    [System.Serializable]
    public struct UTMCoordinate
    {
        public double Easting;   // UTM X (meters)
        public double Northing;  // UTM Y (meters)
        public double Elevation; // Z / RL (meters)

        public UTMCoordinate(double easting, double northing, double elevation)
        {
            Easting = easting;
            Northing = northing;
            Elevation = elevation;
        }

        public override string ToString()
        {
            return $"E: {Easting:F2} m | N: {Northing:F2} m | RL: {Elevation:F2} m";
        }
    }

    /// <summary>
    /// Handles bidirectional conversion between Real-world UTM (Zone 50N) coordinates
    /// and Unity 3D World space coordinates.
    /// </summary>
    public static class GeoCoordinateConverter
    {
        // Same local-world anchor as the backend GPS transform and terrain layer.
        public const double ORIGIN_UTM_X = 572728.3;
        public const double ORIGIN_UTM_Y = 113338.3;
        public const double ORIGIN_UTM_Z = 0.0;
        private const double MAPPED_MIN_EASTING = 570002.52;
        private const double MAPPED_MAX_EASTING = 575454.08;
        private const double MAPPED_MIN_NORTHING = 111294.81;
        private const double MAPPED_MAX_NORTHING = 115381.76;

        public static bool IsInsideMappedTerrain(double easting, double northing)
        {
            return easting >= MAPPED_MIN_EASTING && easting <= MAPPED_MAX_EASTING &&
                   northing >= MAPPED_MIN_NORTHING && northing <= MAPPED_MAX_NORTHING;
        }

        /// <summary>
        /// Converts Real UTM Coordinate to Unity Vector3.
        /// Unity X = Easting - OriginX
        /// Unity Y = Elevation - OriginZ
        /// Unity Z = Northing - OriginY
        /// </summary>
        public static Vector3 UTMToUnity(double easting, double northing, double elevation)
        {
            float unityX = (float)(easting - ORIGIN_UTM_X);
            float unityY = (float)(elevation - ORIGIN_UTM_Z);
            float unityZ = (float)(northing - ORIGIN_UTM_Y);
            return new Vector3(unityX, unityY, unityZ);
        }

        public static Vector3 UTMToUnity(UTMCoordinate utm)
        {
            return UTMToUnity(utm.Easting, utm.Northing, utm.Elevation);
        }

        /// <summary>
        /// Converts Unity Vector3 position back to Real UTM Coordinate.
        /// </summary>
        public static UTMCoordinate UnityToUTM(Vector3 unityPos)
        {
            double easting = ORIGIN_UTM_X + unityPos.x;
            double northing = ORIGIN_UTM_Y + unityPos.z;
            double elevation = ORIGIN_UTM_Z + unityPos.y;
            return new UTMCoordinate(easting, northing, elevation);
        }

        /// <summary>
        /// Converts UTM Zone 50N to approximate WGS84 Latitude and Longitude.
        /// </summary>
        public static void UTMToLatLon(double easting, double northing, out double lat, out double lon)
        {
            // Standard UTM Zone 50N approx projection
            double a = 6378137.0; // WGS84 semi-major axis
            double e = 0.0818191908426; // WGS84 eccentricity
            int zone = 50;
            double falseEasting = 500000.0;
            double k0 = 0.9996;

            double x = easting - falseEasting;
            double y = northing;

            double m = y / k0;
            double mu = m / (a * (1.0 - Math.Pow(e, 2) / 4.0 - 3.0 * Math.Pow(e, 4) / 64.0 - 5.0 * Math.Pow(e, 6) / 256.0));

            double e1 = (1.0 - Math.Sqrt(1.0 - Math.Pow(e, 2))) / (1.0 + Math.Sqrt(1.0 - Math.Pow(e, 2)));

            double phi1 = mu + (3.0 * e1 / 2.0 - 27.0 * Math.Pow(e1, 3) / 32.0) * Math.Sin(2.0 * mu)
                             + (21.0 * Math.Pow(e1, 2) / 16.0 - 55.0 * Math.Pow(e1, 4) / 32.0) * Math.Sin(4.0 * mu)
                             + (151.0 * Math.Pow(e1, 3) / 96.0) * Math.Sin(6.0 * mu);

            double n1 = a / Math.Sqrt(1.0 - Math.Pow(e, 2) * Math.Pow(Math.Sin(phi1), 2));
            double t1 = Math.Tan(phi1) * Math.Tan(phi1);
            double c1 = (Math.Pow(e, 2) / (1.0 - Math.Pow(e, 2))) * Math.Pow(Math.Cos(phi1), 2);
            double r1 = a * (1.0 - Math.Pow(e, 2)) / Math.Pow(1.0 - Math.Pow(e, 2) * Math.Pow(Math.Sin(phi1), 2), 1.5);
            double d = x / (n1 * k0);

            double latRad = phi1 - (n1 * Math.Tan(phi1) / r1) * (Math.Pow(d, 2) / 2.0 - (5.0 + 3.0 * t1 + 10.0 * c1 - 4.0 * Math.Pow(c1, 2) - 9.0 * (Math.Pow(e, 2) / (1.0 - Math.Pow(e, 2)))) * Math.Pow(d, 4) / 24.0);
            double lonRad = ((zone - 1) * 6 - 180 + 3) * Math.PI / 180.0 + (d - (1.0 + 2.0 * t1 + c1) * Math.Pow(d, 3) / 6.0) / Math.Cos(phi1);

            lat = latRad * 180.0 / Math.PI;
            lon = lonRad * 180.0 / Math.PI;
        }
    }
}
