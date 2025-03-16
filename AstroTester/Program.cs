using System;
using AstroHelpers;

namespace AstroTester
{
    class Program
    {
        static void Main()
        {
            // Przykładowa lokalizacja: Warszawa (ok. 52°N, 21°E)
            double latitude = 51.1;
            double longitude = 17.03;

            // Data, dla której chcemy sprawdzić fazę i czasy wsch./zach. Księżyca
            DateTime date = new DateTime(2025, 3, 16);

            // 1. Pobierzemy informacje o Księżycu (faza, wiek, oświetlenie) z klasy MoonCalculator
            var moonInfo = MoonCalculator.GetMoonInfo(date);

            // 2. Obliczymy wschód i zachód Księżyca w podanej lokalizacji, w strefie "Europe/Warsaw"
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
            var (moonrise, moonset) = MoonRiseSetCalculator.GetMoonRiseSetTimes(
                latitude,
                longitude,
                date,
                timeZone
            );

            // 3. Wyświetlamy wyniki
            Console.WriteLine($"Data lokalna: {date:yyyy-MM-dd}");
            Console.WriteLine($"Lokalizacja: {latitude}°N, {longitude}°E\n");

            Console.WriteLine("== Informacje o fazie Księżyca ==");
            Console.WriteLine($"Faza:          {moonInfo.PhaseName}");
            Console.WriteLine($"Wiek (dni):    {moonInfo.AgeInDays:F2}");
            Console.WriteLine($"Oświetlenie:   {moonInfo.IlluminationFraction:P1}");

            Console.WriteLine("\n== Czasy wschodu i zachodu Księżyca ==");
            if (moonrise.HasValue)
                Console.WriteLine($"Wschód Księżyca:  {moonrise.Value:yyyy-MM-dd HH:mm}");
            else
                Console.WriteLine("Wschód Księżyca:  brak (w tej dobie)");

            if (moonset.HasValue)
                Console.WriteLine($"Zachód Księżyca:  {moonset.Value:yyyy-MM-dd HH:mm}");
            else
                Console.WriteLine("Zachód Księżyca:  brak (w tej dobie)");

            Console.WriteLine("\nNaciśnij dowolny klawisz, aby zakończyć.");
            Console.ReadKey();
        }
    }
}
