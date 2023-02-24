using Berg.Configuration;

namespace Berg.Db;

public static class ChallengeLoader
{
    public static void LoadFromConfig(BergDbContext dbContext, CtfInfo info)
    {
        var dbChallenges = dbContext.Challenges.ToList();
        var configChallenges = info.Challenges;
        foreach (var configChallenge in configChallenges)
        {
            var dbChallenge = dbChallenges.FirstOrDefault(c => c.Id == configChallenge.Id);
            if (dbChallenge == null)
            {
                dbContext.Challenges.Add(new Challenge()
                {
                    Id = configChallenge.Id,
                    Name = configChallenge.Name.Trim(),
                    Author = configChallenge.Author,
                    Description = configChallenge.Description.Trim(),
                    Category = configChallenge.Category.Trim(),
                    Flag = configChallenge.Flag.Trim(),
                });
            }
            else
            {
                dbChallenge.Name = configChallenge.Name;
                dbChallenge.Author = configChallenge.Author;
                dbChallenge.Description = configChallenge.Description;
                dbChallenge.Flag = configChallenge.Flag;
                dbChallenge.Category = configChallenge.Category.Trim();
            }
        }

        var challengesToDelete = dbChallenges
            .Where(c => configChallenges.All(i => i.Id != c.Id));
        dbContext.Challenges.RemoveRange(challengesToDelete);
        dbContext.SaveChanges();
    }
}