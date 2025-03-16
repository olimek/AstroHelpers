using System;
using System.Collections.Generic;

namespace AstroHelpers
{
    public static class MoonRiseSetCalculator
    {
        private const double Obliquity = 23.4393;     
        private const double BaseRefractionDeg = 0.57; 
        private const double HorizonOffset = 0.83;     

        public static (DateTime? Moonrise, DateTime? Moonset) GetMoonRiseSetTimes(
            double latitudeDeg, double longitudeDeg,
            DateTime dateLocal, TimeZoneInfo timeZone = null)
        {
            timeZone ??= TimeZoneInfo.Local;

            DateTime local0h = dateLocal.Date;
            DateTime local1h = local0h.AddDays(1);

            DateTime utcDayStart = TimeZoneInfo.ConvertTimeToUtc(local0h, timeZone);
            DateTime utcScanStart = utcDayStart.AddHours(-12);
            DateTime utcScanEnd = utcDayStart.AddHours(36);

            List<(DateTime crossingUtc, bool goingUp)> events = new List<(DateTime, bool)>();

            double prevAlt = GetTopocentricAltitude(utcScanStart, latitudeDeg, longitudeDeg);
            DateTime tPrev = utcScanStart;
            TimeSpan step = TimeSpan.FromMinutes(1);

            for (DateTime t = utcScanStart + step; t <= utcScanEnd; t += step)
            {
                double alt = GetTopocentricAltitude(t, latitudeDeg, longitudeDeg);

                if (prevAlt <= HorizonOffset && alt > HorizonOffset)
                {
                    DateTime crossing = FindAltitudeCrossingBisect(
                        tPrev, prevAlt, t, alt, HorizonOffset, latitudeDeg, longitudeDeg);
                    events.Add((crossing, true));
                }

                if (prevAlt >= HorizonOffset && alt < HorizonOffset)
                {
                    DateTime crossing = FindAltitudeCrossingBisect(
                        tPrev, prevAlt, t, alt, HorizonOffset, latitudeDeg, longitudeDeg);
                    events.Add((crossing, false));
                }

                tPrev = t;
                prevAlt = alt;
            }

            var dayEvents = new List<(DateTime crossingLocal, bool goingUp)>();
            foreach (var ev in events)
            {
                DateTime loc = TimeZoneInfo.ConvertTimeFromUtc(ev.crossingUtc, timeZone);
                if (loc >= local0h && loc < local1h)
                {
                    dayEvents.Add((loc, ev.goingUp));
                }
            }
            dayEvents.Sort((a, b) => a.crossingLocal.CompareTo(b.crossingLocal));

            DateTime? riseLocal = null;
            DateTime? setLocal = null;
            foreach (var eDay in dayEvents)
            {
                if (eDay.goingUp && !riseLocal.HasValue)
                {
                    riseLocal = eDay.crossingLocal;
                }
                else if (!eDay.goingUp && !setLocal.HasValue)
                {
                    setLocal = eDay.crossingLocal;
                }
                if (riseLocal.HasValue && setLocal.HasValue) break;
            }

            return (riseLocal, setLocal);
        }

        private static double GetTopocentricAltitude(DateTime utc, double latDeg, double lonDeg)
        {
            var (mx, my, mz) = GetMoonPositionEquatorialMeeus(utc);
            var (ox, oy, oz) = GetObserverPositionEquatorial(utc, latDeg, lonDeg);

            double tx = mx - ox;
            double ty = my - oy;
            double tz = mz - oz;
            double dist = Math.Sqrt(tx * tx + ty * ty + tz * tz);

            double oLen = Math.Sqrt(ox * ox + oy * oy + oz * oz);
            double nx = ox / oLen;
            double ny = oy / oLen;
            double nz = oz / oLen;

            double dot = tx * nx + ty * ny + tz * nz;
            double cosZen = dot / dist;
            cosZen = Math.Max(-1.0, Math.Min(1.0, cosZen));
            double zen = Math.Acos(cosZen);
            double alt = 90.0 - Rad2Deg(zen);

            return alt;
        }

        private static (double x, double y, double z) GetMoonPositionEquatorialMeeus(DateTime utc)
        {
            double jd = ToJulianDate(utc);
            double T = (jd - 2451545.0) / 36525.0;

            double D = NormalizeDegrees(297.85036 + 445267.11148 * T - 0.0019142 * T * T);
            double M = NormalizeDegrees(357.52772 + 35999.05034 * T - 0.0001603 * T * T);
            double Mp = NormalizeDegrees(134.96298 + 477198.867398 * T + 0.0086972 * T * T);
            double F = NormalizeDegrees(93.27191 + 483202.017538 * T - 0.0036825 * T * T);

            double Drad = Deg2Rad(D);
            double Mrad = Deg2Rad(M);
            double Mprad = Deg2Rad(Mp);
            double Frad = Deg2Rad(F);

            double Lprime = 218.316 + 481267.8813 * T;
            double a = 60.2666; 

            double dL = 0.0, dB = 0.0, dR = 0.0;

            var terms = new (double coeffL, double coeffB, double coeffR,
                             int iD, int iM, int iMp, int iF)[]
            {
                ( +6.2886,  +0.0,     -3.3420,   0,  0, +1,  0),
                ( +1.2740,  +0.0,     -0.3442,  +2,  0, -1,  0),
                ( +0.6583,  +0.0,     -0.0413,  +2,  0,  0,  0),
                ( +0.2136,  +0.0,      0.0,     0,  0, +2,  0),
                ( -0.1851,  +0.0,      0.0,     0, +1,  0,  0),
                ( -0.1143,  +0.0,      0.0,     0,  0,  0, +2),

                (  0.0,    +5.128,   0.0,      0,  0,  0, +1),
                (  0.0,    +0.280,   0.0,      0,  0, +1, +1),
                (  0.0,    +0.277,   0.0,      0,  0, +1, -1),
                (  0.0,    +0.173,   0.0,     +2,  0,  0, -1),
            };

            foreach (var t in terms)
            {
                double arg = (t.iD * Drad + t.iM * Mrad + t.iMp * Mprad + t.iF * Frad);
                double val = Math.Sin(arg);

                dL += t.coeffL * val;
                dB += t.coeffB * val;
                dR += t.coeffR * val;
            }

            double Lmoon = Lprime + dL;
            double Bmoon = dB;
            double Rmoon = a + (dR / 1000.0);

            double lamRad = Deg2Rad(NormalizeDegrees(Lmoon));
            double betRad = Deg2Rad(Bmoon);

            double xecl = Rmoon * Math.Cos(betRad) * Math.Cos(lamRad);
            double yecl = Rmoon * Math.Cos(betRad) * Math.Sin(lamRad);
            double zecl = Rmoon * Math.Sin(betRad);

            double xequat = xecl;
            double yequat = yecl * Math.Cos(Deg2Rad(Obliquity)) - zecl * Math.Sin(Deg2Rad(Obliquity));
            double zequat = yecl * Math.Sin(Deg2Rad(Obliquity)) + zecl * Math.Cos(Deg2Rad(Obliquity));

            return (xequat, yequat, zequat);
        }

        private static (double x, double y, double z) GetObserverPositionEquatorial(
            DateTime utc, double latDeg, double lonDeg)
        {
            double lat = Deg2Rad(latDeg);
            double gmstDeg = GetGMSTInDegrees(utc);

            double localAngleDeg = NormalizeDegrees(gmstDeg + lonDeg);
            double localAngleRad = Deg2Rad(localAngleDeg);

            double cosLat = Math.Cos(lat);
            double x = cosLat * Math.Cos(localAngleRad);
            double y = cosLat * Math.Sin(localAngleRad);
            double z = Math.Sin(lat);

            return (x, y, z);
        }

        private static DateTime FindAltitudeCrossingBisect(
            DateTime t1, double alt1,
            DateTime t2, double alt2,
            double offset,
            double latDeg, double lonDeg)
        {
            DateTime left = t1;
            DateTime right = t2;
            double aLeft = alt1;
            double aRight = alt2;

            for (int i = 0; i < 6; i++)
            {
                var mid = left + TimeSpan.FromSeconds((right - left).TotalSeconds / 2.0);
                double aMid = GetTopocentricAltitude(mid, latDeg, lonDeg);

                if ((aLeft - offset) * (aMid - offset) <= 0)
                {
                    right = mid;
                    aRight = aMid;
                }
                else
                {
                    left = mid;
                    aLeft = aMid;
                }
            }
            return left;
        }

        // ================== Metody astronomiczne pomocnicze ==================

        private static double ToJulianDate(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("DateTime musi być w UTC.");

            int year = utc.Year;
            int month = utc.Month;
            double day = utc.Day
                         + utc.Hour / 24.0
                         + utc.Minute / 1440.0
                         + utc.Second / 86400.0;

            if (month <= 2)
            {
                year--;
                month += 12;
            }
            int A = year / 100;
            int B = 2 - A + (A / 4);

            double jd = Math.Floor(365.25 * (year + 4716))
                        + Math.Floor(30.6001 * (month + 1))
                        + day + B - 1524.5;
            return jd;
        }

        private static double GetGMSTInDegrees(DateTime utc)
        {
            double jd = ToJulianDate(utc);
            double d = jd - 2451545.0;
            double T = d / 36525.0;

            double gmst0h = 6.697374558
                            + 2400.051336 * T
                            + 0.000025862 * (T * T);
            gmst0h = (gmst0h % 24 + 24) % 24;

            double UT = utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0;
            double gmst = gmst0h + UT * 1.002737909;
            gmst = (gmst % 24 + 24) % 24;

            return NormalizeDegrees(gmst * 15.0);
        }

        private static double NormalizeDegrees(double angle)
        {
            double res = angle % 360.0;
            return (res < 0) ? res + 360.0 : res;
        }
        private static double Deg2Rad(double deg) => (Math.PI / 180.0) * deg;
        private static double Rad2Deg(double rad) => (180.0 / Math.PI) * rad;
    }
}
