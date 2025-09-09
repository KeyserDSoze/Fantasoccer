using System.Security.Cryptography;

namespace Fantasoccer.Business
{
    internal sealed class RandomCalendar : IRandomCalendar
    {
        /// <summary>
        /// Genera un calendario round-robin con metodo del cerchio.
        /// - teamCount: numero squadre (se dispari, viene aggiunto un bye).
        /// - round: il numero di “round” logico da associare alle giornate generate.
        /// - doubleRound: se true, genera il girone di ritorno invertendo gli accoppiamenti.
        /// - shuffle: se true, randomizza l’ordine iniziale delle squadre per varietà.
        /// </summary>
        public List<RandomCalendarDay> GetCalendar(int teamCount, int round)
        {
            bool doubleRound = false;
            bool shuffle = true;
            if (teamCount < 2) return new();

            // Prepara l’elenco indici squadra
            var teams = Enumerable.Range(0, teamCount).ToList();

            if (shuffle)
                FisherYatesShuffle(teams);

            // Se dispari, aggiungo un bye (rappresentato da -1)
            var hasBye = (teams.Count % 2 == 1);
            if (hasBye)
                teams.Add(-1);

            int n = teams.Count;                  // n è pari qui
            int days = n - 1;
            int matchesPerDay = n / 2;

            var result = new List<RandomCalendarDay>(days * matchesPerDay);

            // Metodo del cerchio:
            // teams[0] resta “fisso”, ruotano gli altri inserendo l'ultimo in posizione 1 ad ogni giornata.
            for (int day = 1; day <= days; day++)
            {
                for (int i = 0; i < matchesPerDay; i++)
                {
                    int a = teams[i];
                    int b = teams[n - 1 - i];

                    if (a == -1 || b == -1)
                        continue; // uno ha il bye: nessuna partita

                    // Partita d’andata
                    result.Add(new RandomCalendarDay
                    {
                        Day = day,
                        Round = round,
                        Game = new RandomCalendarGame
                        {
                            ATeam = day % 2 == 0 ? a : b,
                            BTeam = day % 2 == 0 ? b : a,
                            Row = i
                        }
                    });

                    if (doubleRound)
                    {
                        // Partita di ritorno (inverti A/B).
                        // Mantengo la stessa “Row” per semplicità; sposto la giornata dopo le (n−1) originali.
                        result.Add(new RandomCalendarDay
                        {
                            Day = day + days,   // giornate del ritorno dopo l’andata
                            Round = round,
                            Game = new RandomCalendarGame
                            {
                                ATeam = day % 2 == 0 ? a : b,
                                BTeam = day % 2 == 0 ? b : a,
                                Row = i
                            }
                        });
                    }
                }

                // Rotazione: mantieni fisso teams[0], sposta l’ultimo in posizione 1
                int last = teams[n - 1];
                teams.RemoveAt(n - 1);
                teams.Insert(1, last);
            }
            result = result.OrderBy(x => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000)).ToList();
            return result;
        }

        public List<AnnualTeam> Randomize(List<AnnualTeam> teams)
        {
            // Fisher–Yates, O(n), nessun while con estrazioni ripetute.
            var copy = new List<AnnualTeam>(teams);
            FisherYatesShuffle(copy);
            return copy;
        }

        private static void FisherYatesShuffle<T>(IList<T> list)
        {
            // Usa RandomNumberGenerator.GetInt32 in modo minimale (O(n) chiamate).
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
