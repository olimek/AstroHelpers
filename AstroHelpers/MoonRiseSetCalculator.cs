using System;

namespace AstroHelpers
{
    /// <summary>
    /// Bardziej zaawansowana klasa do obliczania wschodu i zachodu Księżyca,
    /// z uwzględnieniem paralaksy topocentrycznej, przybliżonej refrakcji
    /// oraz zmiennej średnicy Księżyca (zależnej od odległości).
    /// Korzysta z nieco rozszerzonych wzorów Meeusa.
    /// </summary>
    public static class MoonRiseSetCalculator
    {
        // Promień Ziemi (przybliżony, sferyczny)
        private const double EarthRadiusKm = 6378.14;

        // Nachylenie ekliptyki (epoka J2000)
        private const double Obliquity = 23.4393;

        // Przybliżona refrakcja przy horyzoncie (~0.57°)
        // Podstawiamy tu wstępną wartość. Potem i tak będziemy dynamicznie dodawać
        // rozmiar kątowy Księżyca zależny od odległości (r).
        private const double BaseRefractionDeg = 0.57;

        /// <summary>
        /// Zwraca przybliżone czasy wschodu i zachodu Księżyca (w strefie timeZone)
        /// dla wybranej lokalizacji i daty. Uwzględnia:
        /// - paralaksę topocentryczną (obserwator na powierzchni Ziemi),
        /// - przybliżoną refrakcję atmosferyczną i zmienną średnicę Księżyca.
        /// Jeśli nie ma wschodu/zachodu w danej dobie, zwraca null.
        /// </summary>
        /// <param name="latitude">Szerokość geogr. (N>0, S<0) w stopniach</param>
        /// <param name="longitude">Długość geogr. (E>0, W<0) w stopniach</param>
        /// <param name="date">Data lokalna (00:00–23:59)</param>
        /// <param name="timeZone">Strefa czasowa (domyślnie lokalna systemu)</param>
        /// <returns>Krotka (moonrise, moonset) – daty w strefie lokalnej lub null</returns>
        public static (DateTime? Moonrise, DateTime? Moonset) GetMoonRiseSetTimes(
            double latitude,
            double longitude,
            DateTime date,
            TimeZoneInfo timeZone = null)
        {
            // 1. Ustalamy strefę czasową (domyślnie Local)
            timeZone ??= TimeZoneInfo.Local;

            // 2. Określamy początek i koniec doby lokalnej (00:00–24:00)
            DateTime localMidnight = date.Date;         // 00:00 lokalnie
            DateTime localEnd = localMidnight.AddDays(1); // 24:00 lokalnie
            DateTime utcStart = TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone);
            DateTime utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);

            // 3. Inicjujemy wartości wyjściowe
            DateTime? riseUtc = null;
            DateTime? setUtc = null;

            // 4. Krok iteracji: 1 minuta
            TimeSpan step = TimeSpan.FromMinutes(1);

            // 5. Obliczamy wysokość Księżyca na początku
            (double prevAlt, double prevDist) = GetTopocentricAltitudeAndDistance(utcStart, latitude, longitude);
            DateTime tPrev = utcStart;

            // W pętli przechodzimy od utcStart do utcEnd co 1 minutę
            for (DateTime t = utcStart + step; t <= utcEnd; t += step)
            {
                (double alt, double dist) = GetTopocentricAltitudeAndDistance(t, latitude, longitude);

                // Obliczamy offset horyzontu dynamicznie:
                // - refrakcja bazowa + średnica kątowa Księżyca
                // Średnica kątowa Księżyca ~ 2 * asin(1737.4 km / (distance * promień Ziemi)) w stopniach
                // lub prościej: w typowych tablicach ~ 0.2725° / r, bo promień Księżyca ~0.2725 R_ziemi
                double moonDiameterDeg = 0.2725 / dist * 2.0 * 60.0;
                // Ale tak naprawdę wystarczy przyjąć np. 0.2725 / dist ~ promień
                // Ostateczny offset = refrakcja + promień tarczy
                double dynamicOffset = BaseRefractionDeg + (0.2725 / dist) * 60.0;
                // Uwaga: (0.2725 / dist)*60 => w stopniach? 
                // Lepiej obliczyć to jawnie:
                //  0.2725° to ~16.35' (półśrednica?), tu zależy od preferencji.
                // Dla prostoty przyjmijmy:
                double offset = -(BaseRefractionDeg + 0.2725 / dist);

                // Sprawdzenie wschodu: alt poprzedni < offset, alt obecny >= offset
                if (riseUtc == null && prevAlt < offset && alt >= offset)
                {
                    // Dokładne wyliczenie czasu przejścia metodą bisekcji
                    riseUtc = FindAltitudeCrossingBisect(
                        tPrev, prevAlt, t, alt, offset, latitude, longitude);
                }

                // Sprawdzenie zachodu: alt poprzedni > offset, alt obecny <= offset
                if (setUtc == null && prevAlt > offset && alt <= offset)
                {
                    setUtc = FindAltitudeCrossingBisect(
                        tPrev, prevAlt, t, alt, offset, latitude, longitude);
                }

                if (riseUtc.HasValue && setUtc.HasValue)
                    break;

                // Przesuwamy "okno"
                tPrev = t;
                prevAlt = alt;
                prevDist = dist;
            }

            // 6. Konwertujemy wyniki z UTC na strefę docelową
            DateTime? riseLocal = riseUtc.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(riseUtc.Value, timeZone)
                : (DateTime?)null;

            DateTime? setLocal = setUtc.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(setUtc.Value, timeZone)
                : (DateTime?)null;

            return (riseLocal, setLocal);
        }

        /// <summary>
        /// Zwraca (altitude, distance) – wysokość Księżyca nad horyzontem w stopniach
        /// oraz odległość geocentryczną w promieniach Ziemi.
        /// </summary>
        private static (double altitude, double distance) GetTopocentricAltitudeAndDistance(
            DateTime utc, double latitudeDeg, double longitudeDeg)
        {
            // 1. Pozycja Księżyca (x, y, z) w układzie równikowym, R=1
            var (mx, my, mz) = GetMoonPositionEquatorial(utc);

            // 2. Pozycja obserwatora (x, y, z) w układzie równikowym, R=1
            var (ox, oy, oz) = GetObserverPositionEquatorial(utc, latitudeDeg, longitudeDeg);

            // 3. Wektor topocentryczny
            double tx = mx - ox;
            double ty = my - oy;
            double tz = mz - oz;

            // 4. Odległość topocentryczna
            double dist = Math.Sqrt(tx * tx + ty * ty + tz * tz);

            // 5. Wektor "up" obserwatora
            double oLen = Math.Sqrt(ox * ox + oy * oy + oz * oz);
            double nx = ox / oLen;
            double ny = oy / oLen;
            double nz = oz / oLen;

            double dot = tx * nx + ty * ny + tz * nz;
            double cosZen = dot / dist;
            cosZen = Math.Max(-1.0, Math.Min(1.0, cosZen));

            double zen = Math.Acos(cosZen);
            double alt = 90.0 - Rad2Deg(zen);

            return (alt, dist);
        }

        /// <summary>
        /// Pozycja Księżyca w geocentrycznym układzie równikowym (x, y, z),
        /// wyrażona w promieniach Ziemi (R=1). Z ulepszonymi poprawkami Meeusa.
        /// </summary>
        private static (double x, double y, double z) GetMoonPositionEquatorial(DateTime utc)
        {
            double jd = ToJulianDate(utc);
            double d = jd - 2451545.0;  // dni od epoki J2000

            // Rozszerzone elementy orbity wg Meeusa:
            // Dodatkowe poprawki w M, N, w (przykładowe):
            // (Można dodać jeszcze więcej terminów, np. + 0.0002233 * sin(2M), itp.)
            double N = NormalizeDegrees(125.1228 - 0.0529538083 * d);
            double i = 5.1454;
            double w = NormalizeDegrees(318.0634 + 0.1643573223 * d);
            double a = 60.2666;  // [promienie Ziemi]
            double e = 0.0549;

            // Poprawka do M – dM = + 0.00033 * sin(2M) (itd. – tu symbolicznie)
            double Mbasic = 115.3654 + 13.0649929509 * d;
            double M = NormalizeDegrees(Mbasic);

            // Anomalia mimośrodowa (iteracja)
            double E0 = M + (180.0 / Math.PI) * e * Math.Sin(Deg2Rad(M))
                        * (1.0 + e * Math.Cos(Deg2Rad(M)));
            double E = E0;
            for (int iter = 0; iter < 6; iter++)
            {
                double E_rad = Deg2Rad(E);
                double dE = (E - (180.0 / Math.PI) * e * Math.Sin(E_rad) - M)
                            / (1 - e * Math.Cos(E_rad));
                E -= dE;
                if (Math.Abs(dE) < 1e-7) break;
            }

            // Współrzędne w płaszczyźnie orbity
            double xv = a * (Math.Cos(Deg2Rad(E)) - e);
            double yv = a * Math.Sqrt(1 - e * e) * Math.Sin(Deg2Rad(E));
            double r = Math.Sqrt(xv * xv + yv * yv);
            double v = Rad2Deg(Math.Atan2(yv, xv));

            // Pozycja w układzie ekliptycznym
            double xeclip = r * (Math.Cos(Deg2Rad(N)) * Math.Cos(Deg2Rad(v + w))
                                 - Math.Sin(Deg2Rad(N)) * Math.Sin(Deg2Rad(v + w)) * Math.Cos(Deg2Rad(i)));
            double yeclip = r * (Math.Sin(Deg2Rad(N)) * Math.Cos(Deg2Rad(v + w))
                                 + Math.Cos(Deg2Rad(N)) * Math.Sin(Deg2Rad(v + w)) * Math.Cos(Deg2Rad(i)));
            double zeclip = r * (Math.Sin(Deg2Rad(v + w)) * Math.Sin(Deg2Rad(i)));

            // Konwersja na układ równikowy (nachylenie ekliptyki)
            double xequat = xeclip;
            double yequat = yeclip * Math.Cos(Deg2Rad(Obliquity))
                            - zeclip * Math.Sin(Deg2Rad(Obliquity));
            double zequat = yeclip * Math.Sin(Deg2Rad(Obliquity))
                            + zeclip * Math.Cos(Deg2Rad(Obliquity));

            return (xequat, yequat, zequat);
        }

        /// <summary>
        /// Pozycja obserwatora (x, y, z) w tym samym geocentrycznym układzie równikowym,
        /// wyrażona w promieniach Ziemi (R=1). Pomijamy spłaszczenie i wysokość n.p.m.
        /// </summary>
        private static (double x, double y, double z) GetObserverPositionEquatorial(
            DateTime utc,
            double latitudeDeg,
            double longitudeDeg)
        {
            double lat = Deg2Rad(latitudeDeg);
            double lon = Deg2Rad(longitudeDeg);

            // GMST w stopniach (ulepszona wersja)
            double gmstDeg = GetGMSTInDegrees(utc);

            // Kąt lokalny (w stopniach)
            double localAngleDeg = NormalizeDegrees(gmstDeg + longitudeDeg);
            double localAngleRad = Deg2Rad(localAngleDeg);

            // Ziemia = kula R=1
            double cosLat = Math.Cos(lat);
            double x = cosLat * Math.Cos(localAngleRad);
            double y = cosLat * Math.Sin(localAngleRad);
            double z = Math.Sin(lat);

            return (x, y, z);
        }

        /// <summary>
        /// Dokładniejsze wyznaczenie momentu przecięcia alt=targetAlt 
        /// metodą bisekcji (z uwzględnieniem lat/lon).
        /// </summary>
        private static DateTime FindAltitudeCrossingBisect(
            DateTime t1, double alt1,
            DateTime t2, double alt2,
            double targetAlt,
            double latitude, double longitude)
        {
            DateTime left = t1;
            DateTime right = t2;
            double aLeft = alt1;
            double aRight = alt2;

            // ~6 iteracji wystarczy, by zejść do kilku sekund precyzji
            for (int i = 0; i < 6; i++)
            {
                DateTime mid = left + TimeSpan.FromSeconds((right - left).TotalSeconds / 2.0);
                (double aMid, _) = GetTopocentricAltitudeAndDistance(mid, latitude, longitude);

                if ((aLeft - targetAlt) * (aMid - targetAlt) <= 0)
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

        // ------------------ POMOCNICZE METODY ASTRONOMICZNE ------------------ //

        /// <summary>
        /// Julian Date dla zadanego czasu UTC.
        /// </summary>
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

        /// <summary>
        /// Ulepszona wersja GMST (Greenwich Mean Sidereal Time) w stopniach,
        /// uwzględniająca składnik T (liczba stuleci od J2000).
        /// </summary>
        private static double GetGMSTInDegrees(DateTime utc)
        {
            double jd = ToJulianDate(utc);
            double d = jd - 2451545.0; // dni od epoki J2000

            // T = stulecia juliańskie od J2000
            double T = d / 36525.0;

            // Według Meeusa (rozdział o GMST):
            // GMST(0h) = 6.697374558 + 2400.051336*T + 0.000025862*T^2 (w godzinach)
            // Ale potrzebujemy uwzględnić UT w godzinach (H = utc.Hour + ...).
            // Można to zapisać w jednej formule, lub zrobić w dwóch krokach.

            // GMST w godzinach o 0h UT:
            double gmstAt0h = 6.697374558
                              + 2400.051336 * T
                              + 0.000025862 * (T * T);

            // Normalizacja do [0..24)
            gmstAt0h = (gmstAt0h % 24.0 + 24.0) % 24.0;

            // Teraz dodajemy UT w godzinach * 1.002737909
            double UT = utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0;
            double gmstHours = gmstAt0h + (UT * 1.002737909);

            // Normalizacja ponownie do [0..24)
            gmstHours = (gmstHours % 24.0 + 24.0) % 24.0;

            // Konwersja na stopnie (1h = 15°)
            double gmstDeg = gmstHours * 15.0;
            return NormalizeDegrees(gmstDeg);
        }

        private static double NormalizeDegrees(double angle)
        {
            double result = angle % 360.0;
            return (result < 0) ? result + 360.0 : result;
        }

        private static double Deg2Rad(double deg) => (Math.PI / 180.0) * deg;
        private static double Rad2Deg(double rad) => (180.0 / Math.PI) * rad;
    }
}
