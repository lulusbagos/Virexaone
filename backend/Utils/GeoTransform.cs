using System;
using Microsoft.Extensions.Configuration;
using Virexaone.FMS.Backend.Models;

namespace Virexaone.FMS.Backend.Utils
{
    public static class GeoTransform
    {
        public static double RefEasting { get; private set; } = 572728.3;
        public static double RefNorthing { get; private set; } = 113338.3;
        public static double RefElevation { get; private set; } = 71.0;

        // Hexagon Grid Reference Constants
        public static double HexOriginX { get; private set; } = 423565000.0;
        public static double HexOriginY { get; private set; } = 3680000.0;
        public static double HexScaleM { get; private set; } = 0.03048;
        public static double SiteLat { get; private set; } = 1.022302;
        public static double SiteLon { get; private set; } = 117.657014;
        public static int UtmEpsg { get; private set; } = 32650;
        public const double MetersPerDegLat = 111132.954;
        public static double MetersPerDegLon => 111412.84 * Math.Cos(SiteLat * Math.PI / 180.0);

        public static void Configure(IConfigurationSection settings)
        {
            int epsg = settings.GetValue("UtmEpsg", 32650);
            if (epsg is < 32601 or > 32760 || (epsg > 32660 && epsg < 32701))
                throw new InvalidOperationException("FmsSettings:UtmEpsg must be WGS84 UTM EPSG 32601-32660 or 32701-32760.");

            double scale = settings.GetValue("HexScaleM", 0.03048);
            double lat = settings.GetValue("SiteLat", 1.022302);
            double lon = settings.GetValue("SiteLon", 117.657014);
            if (!double.IsFinite(scale) || scale <= 0 || !double.IsFinite(lat) || Math.Abs(lat) >= 90 ||
                !double.IsFinite(lon) || Math.Abs(lon) > 180)
                throw new InvalidOperationException("Invalid geospatial reference settings.");

            RefEasting = settings.GetValue("RefEasting", 572728.3);
            RefNorthing = settings.GetValue("RefNorthing", 113338.3);
            RefElevation = settings.GetValue("RefElevation", 71.0);
            HexOriginX = settings.GetValue("HexOriginX", 423565000.0);
            HexOriginY = settings.GetValue("HexOriginY", 3680000.0);
            HexScaleM = scale;
            SiteLat = lat;
            SiteLon = lon;
            UtmEpsg = epsg;
        }

        public static bool IsWithinSiteRadius(double latitude, double longitude, double radiusKm)
        {
            if (!double.IsFinite(radiusKm) || radiusKm <= 0) return false;
            double latDelta = (latitude - SiteLat) * Math.PI / 180.0;
            double lonDelta = (longitude - SiteLon) * Math.PI / 180.0;
            double a = Math.Pow(Math.Sin(latDelta / 2.0), 2) +
                Math.Cos(SiteLat * Math.PI / 180.0) * Math.Cos(latitude * Math.PI / 180.0) *
                Math.Pow(Math.Sin(lonDelta / 2.0), 2);
            return 12742.0 * Math.Asin(Math.Min(1.0, Math.Sqrt(a))) <= radiusKm;
        }

        /// <summary>
        /// Transforms raw Hexagon Survey coordinates into WGS84 Lat/Lng, UTM Easting/Northing, and Unity Local (X, Y, Z).
        /// </summary>
        public static (double Lat, double Lon, double Easting, double Northing, double Elevation, UnityVector3 UnityPos) HexagonToWorld(
            double? rawX, double? rawY, double? rawZ)
        {
            if (!rawX.HasValue || !rawY.HasValue ||
                (Math.Abs(rawX.Value) < 0.000001 && Math.Abs(rawY.Value) < 0.000001))
            {
                return (SiteLat, SiteLon, RefEasting, RefNorthing, RefElevation, new UnityVector3(0, RefElevation, 0));
            }

            double numX = rawX.Value;
            double numY = rawY.Value;
            double numZ = rawZ ?? 0.0;

            // Source records may already contain WGS84 longitude and latitude.
            if (numX >= -180.0 && numX <= 180.0 && numY >= -90.0 && numY <= 90.0)
            {
                var (uEasting, uNorthing) = Wgs84ToUtm(numX, numY);
                double uElev = numZ > 0 ? numZ : RefElevation;
                var uPos = UtmToUnityPos(uEasting, uNorthing, uElev);
                return (numY, numX, uEasting, uNorthing, uElev, uPos);
            }

            // Hexagon Desifoot relative transformation
            double xMeters = (numX - HexOriginX) * HexScaleM;
            double zMeters = (numY - HexOriginY) * HexScaleM;
            double elevation = numZ > 5000.0 ? numZ / 100.0 : (numZ > 0.0 ? numZ * HexScaleM : RefElevation);

            double lat = SiteLat + (zMeters / MetersPerDegLat);
            double lon = SiteLon + (xMeters / MetersPerDegLon);

            double easting = Math.Round(RefEasting + xMeters, 2);
            double northing = Math.Round(RefNorthing + zMeters, 2);
            elevation = Math.Round(elevation, 2);

            var unityPos = new UnityVector3(Math.Round(xMeters, 2), elevation, Math.Round(zMeters, 2));

            return (Math.Round(lat, 6), Math.Round(lon, 6), easting, northing, elevation, unityPos);
        }

        /// <summary>
        /// WGS84 Lat/Lng to the UTM zone configured for this deployment.
        /// </summary>
        public static (double Easting, double Northing) Wgs84ToUtm(double lon, double lat)
        {
            if (Math.Abs(lon) < 0.0001 && Math.Abs(lat) < 0.0001)
                return (RefEasting, RefNorthing);

            const double a = 6378137.0;
            const double e2 = 0.081819190842622 * 0.081819190842622;
            const double k0 = 0.9996;

            double latRad = lat * Math.PI / 180.0;
            double lonRad = lon * Math.PI / 180.0;
            int zone = UtmEpsg % 100;
            double lonOrigin = (zone * 6.0 - 183.0) * Math.PI / 180.0;
            double dLon = lonRad - lonOrigin;

            double N = a / Math.Sqrt(1.0 - e2 * Math.Sin(latRad) * Math.Sin(latRad));
            double T = Math.Tan(latRad) * Math.Tan(latRad);
            double C = (e2 / (1.0 - e2)) * Math.Cos(latRad) * Math.Cos(latRad);
            double A = Math.Cos(latRad) * dLon;

            double M = a * ((1.0 - e2 / 4.0 - 3.0 * e2 * e2 / 64.0 - 5.0 * e2 * e2 * e2 / 256.0) * latRad
                - (3.0 * e2 / 8.0 + 3.0 * e2 * e2 / 32.0 + 45.0 * e2 * e2 * e2 / 1024.0) * Math.Sin(2.0 * latRad)
                + (15.0 * e2 * e2 / 256.0 + 45.0 * e2 * e2 * e2 / 1024.0) * Math.Sin(4.0 * latRad)
                - (35.0 * e2 * e2 * e2 / 3072.0) * Math.Sin(6.0 * latRad));

            double easting = k0 * N * (A + (1.0 - T + C) * A * A * A / 6.0
                + (5.0 - 18.0 * T + T * T + 72.0 * C - 58.0 * (e2 / (1.0 - e2))) * A * A * A * A * A / 120.0) + 500000.0;

            double northing = k0 * (M + N * Math.Tan(latRad) * (A * A / 2.0
                + (5.0 - T + 9.0 * C + 4.0 * C * C) * A * A * A * A / 24.0
                + (61.0 - 58.0 * T + T * T + 600.0 * C - 330.0 * (e2 / (1.0 - e2))) * A * A * A * A * A * A / 720.0));

            if (UtmEpsg >= 32701) northing += 10000000.0;

            return (Math.Round(easting, 2), Math.Round(northing, 2));
        }

        public static UnityVector3 UtmToUnityPos(double easting, double northing, double elevation = 71.0)
        {
            double unityX = easting - RefEasting;
            double unityZ = northing - RefNorthing;
            double unityY = elevation;

            return new UnityVector3(Math.Round(unityX, 2), Math.Round(unityY, 2), Math.Round(unityZ, 2));
        }
    }
}
