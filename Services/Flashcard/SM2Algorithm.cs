namespace BambooBrain_Service.Services.Flashcard
{
    public static class SM2Algorithm
    {
        // Grade meanings:
        // 5 - Perfect response
        // 4 - Correct with hesitation
        // 3 - Correct with difficulty
        // 2 - Incorrect but easy to recall
        // 1 - Incorrect, remembered on seeing answer
        // 0 - Complete blackout

        public static Models.Flashcard Calculate(Models.Flashcard card, int grade)
        {
            if (grade < 0 || grade > 5)
                throw new ArgumentException("Grade must be between 0 and 5");

            card.LastReviewDate = DateTime.UtcNow;
            card.LastGrade = grade;
            card.TotalReviews++;

            if (grade >= 3)
            {
                // Correct response
                card.CorrectStreak++;

                card.IntervalDays = card.Repetitions switch
                {
                    0 => 1,
                    1 => 6,
                    _ => (int)Math.Round(card.IntervalDays * card.EaseFactor)
                };

                card.Repetitions++;
            }
            else
            {
                // Incorrect response — reset repetitions
                card.Repetitions = 0;
                card.IntervalDays = 1;
                card.CorrectStreak = 0;
            }

            // Update ease factor
            // EF' = EF + (0.1 - (5-grade) * (0.08 + (5-grade) * 0.02))
            card.EaseFactor = Math.Max(
                1.3,
                card.EaseFactor + 0.1 - (5 - grade) * (0.08 + (5 - grade) * 0.02)
            );

            // Schedule next review
            card.NextReviewDate = DateTime.UtcNow.AddDays(card.IntervalDays);

            return card;
        }
    }
}
